from datetime import datetime, timedelta
from functools import lru_cache
import os
from pathlib import Path
from typing import Optional
import time
import copy
import subprocess

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
import joblib
import numpy as np
import pandas as pd
from pydantic import BaseModel, Field
import requests as req_lib
from demand_features import build_inference_row

BASE_DIR = Path(__file__).resolve().parent


def _load_historical_df() -> pd.DataFrame:
    path = BASE_DIR.parent / "Import Data" / "cleaned_data.csv"
    try:
        if path.exists():
            df = pd.read_csv(path)
            df.columns = df.columns.str.strip().str.replace(" ", "_")
            print(f"Historical price data loaded from: {path}")
            return df
    except Exception as e:
        print(f"Error loading historical price data: {e}")
    return pd.DataFrame()

def _load_inventory_df() -> pd.DataFrame:
    path = BASE_DIR.parent / "Import Data" / "crop_shelf_life_500000.csv"
    try:
        if path.exists():
            df = pd.read_csv(path)
            df.columns = df.columns.str.strip().str.replace(" ", "_")
            print(f"Inventory data loaded from: {path}")
            return df
    except Exception as e:
        print(f"Error loading inventory data: {e}")
    return pd.DataFrame()

# --- AUTO-TRAIN LOGIC FOR GITHUB DEPLOYMENTS ---
price_model_path = BASE_DIR / "price_model.pkl"
if not price_model_path.exists():
    print("⚠️ price_model.pkl not found! Automatically training it on the fly...")
    subprocess.run(["python", str(BASE_DIR / "train_price_model.py")], check=True)

print("Loading price model...")
try:
    model = joblib.load(price_model_path)
    print("Price model loaded.")
except Exception:
    model = None
    print("Price model not found even after training attempt.")

demand_model_path = BASE_DIR / "demand_model.pkl"
if not demand_model_path.exists():
    print("⚠️ demand_model.pkl not found! Automatically training it on the fly...")
    subprocess.run(["python", str(BASE_DIR / "train_demand_model.py")], check=True)

print("Loading demand model...")
try:
    demand_model = joblib.load(demand_model_path)
    print("Demand model loaded.")
except Exception:
    demand_model = None
    print("Demand model not found. Using fallback demand logic.")

shelf_life_model_path = BASE_DIR / "shelf_life_model.pkl"
if not shelf_life_model_path.exists():
    print("⚠️ shelf_life_model.pkl not found! Automatically training it on the fly...")
    subprocess.run(["python", str(BASE_DIR / "train_self_life.py")], check=True)

print("Loading shelf life model...")
try:
    shelf_life_model = joblib.load(shelf_life_model_path)
    produce_encoder = joblib.load(BASE_DIR / "produce_encoder.pkl")
    print("Shelf life model and encoder loaded.")
except Exception:
    shelf_life_model = None
    produce_encoder = None
    print("Shelf life model not found. Using math fallback logic.")

print("Loading local historical data...")
_df = _load_historical_df()
if _df.empty:
    print("Historical price data not found in Import Data.")

print("Loading local inventory data...")
_inventory_df = _load_inventory_df()
if _inventory_df.empty:
    print("Inventory data not found in Import Data.")

app = FastAPI(title="Agricultural Price Prediction API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

DATA_GOV_API_KEY = os.getenv(
    "DATA_GOV_API_KEY",
    "579b464db66ec23bdd000001aac5b9cfd342444773dadcfdaba7266b",
)
DATA_GOV_URL = "https://api.data.gov.in/resource/9ef84268-d588-465a-a308-a864a43d0070"
API_LIMIT = "500"  # max rows pulled when we truly need wide state-level scans
API_LIMIT_DROPDOWNS = "200"  # dropdowns don't need huge payloads
API_LIMIT_ADMIN = "500"  # admin insights: enough variety, less latency
ENDPOINT_CACHE_TTL_SECONDS = 600
_endpoint_cache = {}
GOV_CACHE_TTL_SECONDS = 3600
GOV_BACKOFF_SECONDS = 300
_gov_cache = {}
_gov_backoff_until = 0.0


class FarmerPredictRequest(BaseModel):
    STATE: str = Field(alias="state")
    Commodity: str = Field(alias="commodity")
    Market_Name: str = Field(alias="market")
    prev_price: float = 0.0  # Optional in the new UI, we fetch it live


class PredictResponse(BaseModel):
    predicted_price: float
    predicted_price_per_kg: Optional[float] = None
    month: int
    day: int
    year: int
    avg_price_7: float
    avg_price_7_per_kg: Optional[float] = None
    Min_Price: float
    Min_Price_per_kg: Optional[float] = None
    Max_Price: float
    Max_Price_per_kg: Optional[float] = None
    source: str
    demand_score: Optional[float] = None
    demand_label: Optional[str] = None


class AdminPredictRequest(BaseModel):
    state: str
    market: str


def _get_endpoint_cache(key):
    entry = _endpoint_cache.get(key)
    if not entry:
        return None
    expiry_ts, payload = entry
    if time.time() > expiry_ts:
        _endpoint_cache.pop(key, None)
        return None
    return copy.deepcopy(payload)


def _set_endpoint_cache(key, payload, ttl_seconds: int = ENDPOINT_CACHE_TTL_SECONDS):
    _endpoint_cache[key] = (time.time() + ttl_seconds, copy.deepcopy(payload))


def _commodities_from_state_records(state_records):
    return sorted({r.get("commodity", "").strip() for r in state_records if r.get("commodity")})


def _normalize_market_name(name: str) -> str:
    return " ".join((name or "").strip().lower().split())


def _demand_label(score: float) -> str:
    if score > 0.25:
        return "very_high"
    if score > 0.10:
        return "increasing"
    if score < -0.10:
        return "low"
    return "stable"


def _to_per_kg(price_value: float) -> float:
    # data.gov mandi prices are typically per quintal (100 kg)
    return round(float(price_value) / 100.0, 2)


def _season_recommendation(score_90d: float, score_30d: float) -> str:
    if score_90d >= 0.20:
        return "Highly Profitable (Good for Sowing)"
    if score_90d >= 0.08 or score_30d >= 0.10:
        return "Stable (Monitor Market)"
    if score_90d <= -0.15:
        return "High Risk (Avoid Sowing)"
    return "Moderate Risk (Proceed with Caution)"


def _predict_demand_growth(
    state: str,
    market: str,
    commodity: str,
    target_date: datetime,
    current_price: float,
    min_price: float,
    max_price: float,
    avg_price_7: float,
) -> Optional[float]:
    if demand_model is None:
        return None
    try:
        row = build_inference_row(
            state=state,
            market=market,
            commodity=commodity,
            target_date=pd.Timestamp(target_date),
            min_price=min_price,
            max_price=max_price,
            avg_price_7=avg_price_7,
            prev_price=current_price,
        )
        growth = float(demand_model.predict(row)[0])
        return float(np.clip(growth, -0.9, 3.0))
    except Exception:
        return None

def _fallback_to_df(state: str, commodity: str, market: str, limit: str):
    if _df.empty:
        return ()
    temp_df = _df
    if state:
        temp_df = temp_df[temp_df["STATE"].astype(str).str.lower() == state.lower()]
    if commodity:
        temp_df = temp_df[temp_df["Commodity"].astype(str).str.lower() == commodity.lower()]
    if market:
        temp_df = temp_df[temp_df["Market_Name"].astype(str).str.lower() == market.lower()]
        
    try:
        limit_val = int(limit)
    except ValueError:
        limit_val = 1000
        
    temp_df = temp_df.head(limit_val)
    records = []
    for _, row in temp_df.iterrows():
        records.append({
            "state": str(row.get("STATE", "")),
            "commodity": str(row.get("Commodity", "")),
            "market": str(row.get("Market_Name", "")),
            "min_price": float(row.get("Min_Price", 0) or 0),
            "max_price": float(row.get("Max_Price", 0) or 0),
            "modal_price": float(row.get("Modal_Price", 0) or 0),
            "variety": str(row.get("Variety", "")),
            "arrival_date": f"{int(row.get('day', 1)):02d}/{int(row.get('month', 1)):02d}/{int(row.get('year', 2024))}"
        })
    return tuple(records)

def _fetch_records_cached(
    state: str = "",
    commodity: str = "",
    market: str = "",
    limit: str = API_LIMIT,
):
    global _gov_backoff_until

    cache_key = (state.strip().lower(), commodity.strip().lower(), market.strip().lower(), str(limit))
    now = time.time()

    cached = _gov_cache.get(cache_key)
    if cached and now <= cached[0]:
        return cached[1]

    if now < _gov_backoff_until:
        # During backoff (rate limit / transient failure), prefer returning the last cached
        # payload even if it's stale, so dropdowns don't randomly go empty.
        if cached:
            return cached[1]
        return _fallback_to_df(state, commodity, market, limit)



    params = {
        "api-key": DATA_GOV_API_KEY,
        "format": "json",
        "limit": str(limit),
    }
    if state:
        params["filters[state]"] = state
    if commodity:
        params["filters[commodity]"] = commodity
    if market:
        params["filters[market]"] = market

    try:
        res = req_lib.get(DATA_GOV_URL, params=params, timeout=30)
        res.raise_for_status()
        records = tuple(res.json().get("records", []))
        _gov_cache[cache_key] = (now + GOV_CACHE_TTL_SECONDS, records)
        return records
    except Exception as e:
        print(f"Gov API Rate Limit/Error: {e}")
        _gov_backoff_until = now + GOV_BACKOFF_SECONDS
        print("Falling back to local CSV data...")
        return _fallback_to_df(state, commodity, market, limit)


@app.get("/dropdowns/states")
def get_states():
    records = _fetch_records_cached(limit=API_LIMIT_DROPDOWNS)
    states = {r.get("state", "").strip() for r in records if r.get("state")}
    if len(states) <= 1 and not _df.empty and "STATE" in _df.columns:
        states.update(_df["STATE"].dropna().unique())
    return {"data": sorted(states)}


@app.get("/dropdowns/commodities")
def get_commodities(state: str = "", market: str = ""):
    if state:
        records = list(_fetch_records_cached(state=state, limit=API_LIMIT_DROPDOWNS))
    else:
        records = list(_fetch_records_cached(limit=API_LIMIT_DROPDOWNS))

    if market:
        market_norm = _normalize_market_name(market)
        records = [
            r for r in records
            if _normalize_market_name(r.get("market", "")) == market_norm
        ]

    commodities = {
        r.get("commodity", "").strip()
        for r in records
        if r.get("commodity")
    }
    
    if len(commodities) <= 1 and not _df.empty and "Commodity" in _df.columns:
        temp_df = _df
        if state:
            temp_df = temp_df[temp_df["STATE"].str.strip().str.lower() == state.strip().lower()]
        if market:
            market_norm = _normalize_market_name(market)
            temp_df = temp_df[temp_df["Market_Name"].fillna("").apply(_normalize_market_name) == market_norm]
        commodities.update(temp_df["Commodity"].dropna().unique())
        
    return {"data": sorted(commodities)}


@app.get("/dropdowns/markets")
def get_markets(state: str = ""):
    records = (
        _fetch_records_cached(state=state, limit=API_LIMIT_DROPDOWNS)
        if state
        else _fetch_records_cached(limit=API_LIMIT_DROPDOWNS)
    )
    markets = {
        r.get("market", "").strip()
        for r in records
        if r.get("market") and r.get("market").lower() != "unknown"
    }
    
    if len(markets) <= 1 and not _df.empty and "Market_Name" in _df.columns:
        temp_df = _df
        if state:
            temp_df = temp_df[temp_df["STATE"].str.strip().str.lower() == state.strip().lower()]
        markets.update(temp_df["Market_Name"].dropna().unique())
        
    return {"data": sorted(markets)}


def fetch_live_prices(commodity: str, market: str, state: str):
    try:
        records = list(_fetch_records_cached(state=state, commodity=commodity))
        if not records:
            return None

        market_clean = market.strip().lower()
        exact_market = [r for r in records if r.get("market", "").strip().lower() == market_clean]
        final = exact_market if exact_market else records

        min_p = [float(r["min_price"]) for r in final if r.get("min_price")]
        max_p = [float(r["max_price"]) for r in final if r.get("max_price")]
        modal = [float(r["modal_price"]) for r in final if r.get("modal_price")]
        if not modal:
            return None

        variety_candidates = [r.get("variety", "").strip() for r in final if r.get("variety")]
        chosen_variety = variety_candidates[0] if variety_candidates else "Unknown"

        return {
            "Min_Price": float(np.mean(min_p)) if min_p else float(np.mean(modal)),
            "Max_Price": float(np.mean(max_p)) if max_p else float(np.mean(modal)),
            "avg_price_7": float(np.mean(modal)),
            "source": "live (market exact)" if exact_market else "live (state avg)",
            "Variety": chosen_variety,
        }
    except Exception as exc:
        print("Live API error:", exc)
        return None


def fetch_historical_prices(commodity: str, market: str, state: str):
    if _df.empty or "Commodity" not in _df.columns or "STATE" not in _df.columns:
        return None
    df = _df.copy()
    df = df[
        (df["Commodity"].fillna("").str.strip().str.lower() == commodity.strip().lower())
        & (df["STATE"].fillna("").str.strip().str.lower() == state.strip().lower())
    ]
    if market:
        market_norm = _normalize_market_name(market)
        df = df[df["Market_Name"].fillna("").apply(_normalize_market_name) == market_norm]
    if len(df) == 0:
        return None

    chosen_variety = (
        df["Variety"].mode().iloc[0]
        if "Variety" in df.columns and not df["Variety"].dropna().empty
        else "Unknown"
    )
    return {
        "Min_Price": float(df["Min_Price"].mean()),
        "Max_Price": float(df["Max_Price"].mean()),
        "avg_price_7": float(df["Modal_Price"].mean()),
        "source": "historical",
        "Variety": str(chosen_variety),
    }


def _get_prices_with_fallback(commodity: str, market: str, state: str):
    prices = fetch_live_prices(commodity, market, state)
    if prices is None:
        prices = fetch_historical_prices(commodity, market, state)
    return prices


def _build_state_price_index(state_records):
    index = {}
    for r in state_records:
        commodity = r.get("commodity", "").strip()
        if not commodity:
            continue
        if commodity not in index:
            index[commodity] = {"min": [], "max": [], "modal": [], "variety": []}
        bucket = index[commodity]
        if r.get("min_price"):
            bucket["min"].append(float(r["min_price"]))
        if r.get("max_price"):
            bucket["max"].append(float(r["max_price"]))
        if r.get("modal_price"):
            bucket["modal"].append(float(r["modal_price"]))
        if r.get("variety"):
            bucket["variety"].append(r.get("variety", "").strip())
    return index


def _state_prices_from_index(state_price_index: dict, commodity: str):
    item = state_price_index.get(commodity)
    if not item or not item["modal"]:
        return None
    chosen_variety = item["variety"][0] if item["variety"] else "Unknown"
    return {
        "Min_Price": float(np.mean(item["min"])) if item["min"] else float(np.mean(item["modal"])),
        "Max_Price": float(np.mean(item["max"])) if item["max"] else float(np.mean(item["modal"])),
        "avg_price_7": float(np.mean(item["modal"])),
        "source": "live (state avg)",
        "Variety": chosen_variety,
    }


def _get_state_level_prices_with_fallback(
    commodity: str,
    state: str,
    state_records=None,
    state_price_index=None,
):
    prices = None
    if state_price_index is not None:
        prices = _state_prices_from_index(state_price_index, commodity)
    elif state_records is not None:
        prices = _state_prices_from_index(_build_state_price_index(state_records), commodity)
    else:
        records = list(_fetch_records_cached(state=state, commodity=commodity))
        if records:
            prices = _state_prices_from_index(_build_state_price_index(records), commodity)

    if prices is not None:
        return prices

    if _df.empty or "Commodity" not in _df.columns or "STATE" not in _df.columns:
        return None
    df = _df[
        (_df["Commodity"].fillna("").str.strip().str.lower() == commodity.strip().lower())
        & (_df["STATE"].fillna("").str.strip().str.lower() == state.strip().lower())
    ]
    if len(df) == 0:
        return None
    return {
        "Min_Price": float(df["Min_Price"].mean()),
        "Max_Price": float(df["Max_Price"].mean()),
        "avg_price_7": float(df["Modal_Price"].mean()),
        "source": "historical (state avg)",
        "Variety": (
            df["Variety"].mode().iloc[0]
            if "Variety" in df.columns and not df["Variety"].dropna().empty
            else "Unknown"
        ),
    }


def _aggregate_prices_from_records(records, commodity: str, market: str):
    if not records:
        return None

    commodity_records = [r for r in records if r.get("commodity", "").strip() == commodity]
    if not commodity_records:
        return None

    market_clean = market.strip().lower()
    exact_market = [
        r for r in commodity_records
        if r.get("market", "").strip().lower() == market_clean
    ]
    final = exact_market if exact_market else commodity_records

    min_p = [float(r["min_price"]) for r in final if r.get("min_price")]
    max_p = [float(r["max_price"]) for r in final if r.get("max_price")]
    modal = [float(r["modal_price"]) for r in final if r.get("modal_price")]
    if not modal:
        return None

    variety_candidates = [r.get("variety", "").strip() for r in final if r.get("variety")]
    chosen_variety = variety_candidates[0] if variety_candidates else "Unknown"

    return {
        "Min_Price": float(np.mean(min_p)) if min_p else float(np.mean(modal)),
        "Max_Price": float(np.mean(max_p)) if max_p else float(np.mean(modal)),
        "avg_price_7": float(np.mean(modal)),
        "source": "live (market exact)" if exact_market else "live (state avg)",
        "Variety": chosen_variety,
    }


def _build_price_index_for_market(state_records, market: str):
    market_clean = market.strip().lower()
    index = {}

    def ensure_bucket(commodity_name):
        if commodity_name not in index:
            index[commodity_name] = {
                "state": {"min": [], "max": [], "modal": [], "variety": []},
                "market": {"min": [], "max": [], "modal": [], "variety": []},
            }
        return index[commodity_name]

    for r in state_records:
        commodity = r.get("commodity", "").strip()
        if not commodity:
            continue
        bucket = ensure_bucket(commodity)

        min_raw = r.get("min_price")
        max_raw = r.get("max_price")
        modal_raw = r.get("modal_price")
        variety = r.get("variety", "").strip()

        if min_raw:
            bucket["state"]["min"].append(float(min_raw))
        if max_raw:
            bucket["state"]["max"].append(float(max_raw))
        if modal_raw:
            bucket["state"]["modal"].append(float(modal_raw))
        if variety:
            bucket["state"]["variety"].append(variety)

        if r.get("market", "").strip().lower() == market_clean:
            if min_raw:
                bucket["market"]["min"].append(float(min_raw))
            if max_raw:
                bucket["market"]["max"].append(float(max_raw))
            if modal_raw:
                bucket["market"]["modal"].append(float(modal_raw))
            if variety:
                bucket["market"]["variety"].append(variety)

    return index


def _prices_from_index(price_index: dict, commodity: str):
    item = price_index.get(commodity)
    if not item:
        return None

    market_modal = item["market"]["modal"]
    state_modal = item["state"]["modal"]
    use_market = len(market_modal) > 0
    chosen = item["market"] if use_market else item["state"]

    if not chosen["modal"]:
        return None

    chosen_variety = chosen["variety"][0] if chosen["variety"] else "Unknown"
    return {
        "Min_Price": float(np.mean(chosen["min"])) if chosen["min"] else float(np.mean(chosen["modal"])),
        "Max_Price": float(np.mean(chosen["max"])) if chosen["max"] else float(np.mean(chosen["modal"])),
        "avg_price_7": float(np.mean(chosen["modal"])),
        "source": "live (market exact)" if use_market else "live (state avg)",
        "Variety": chosen_variety,
    }


def _get_prices_with_bulk_fallback(
    commodity: str,
    market: str,
    state: str,
    state_records=None,
    price_index=None,
):
    prices = None
    if price_index is not None:
        prices = _prices_from_index(price_index, commodity)
    elif state_records is not None:
        prices = _aggregate_prices_from_records(state_records, commodity, market)

    if prices is None:
        prices = fetch_historical_prices(commodity, market, state)
    return prices


def _build_model_input(state: str, commodity: str, market: str, prev_price: float, prices: dict, target_date: datetime):
    min_price = float(prices.get("Min_Price", 0.0))
    if np.isnan(min_price): min_price = prices["avg_price_7"]
    max_price = float(prices.get("Max_Price", 0.0))
    if np.isnan(max_price): max_price = prices["avg_price_7"]
    avg_price_7 = float(prices.get("avg_price_7", 0.0))
    variety = prices.get("Variety", "Unknown")

    price_range = max_price - min_price
    price_center = (max_price + min_price) / 2
    price_momentum = prev_price - avg_price_7
    range_ratio = price_range / price_center if price_center else 0.0
    month_sin = np.sin(2 * np.pi * target_date.month / 12)
    month_cos = np.cos(2 * np.pi * target_date.month / 12)

    return pd.DataFrame(
        [
            {
                "STATE": state,
                "Commodity": commodity,
                "Variety": variety,
                "Market_Name": market,
                "month": target_date.month,
                "day": target_date.day,
                "year": target_date.year,
                "Min_Price": min_price,
                "Max_Price": max_price,
                "price_range": price_range,
                "price_center": price_center,
                "prev_price": prev_price,
                "avg_price_7": avg_price_7,
                "price_momentum": price_momentum,
                "range_ratio": range_ratio,
                "month_sin": month_sin,
                "month_cos": month_cos,
            }
        ]
    )


def _predict_price(state: str, commodity: str, market: str, prev_price: float, target_date: Optional[datetime] = None):
    if target_date is None:
        target_date = datetime.today()

    prices = _get_prices_with_fallback(commodity, market, state)
    if prices is None:
        # Fallback to prevent 404 UI crash
        prices = {
            "Min_Price": 20.0,
            "Max_Price": 40.0,
            "avg_price_7": 30.0,
            "source": "simulated fallback",
            "Variety": "Unknown"
        }

    if prev_price == 0.0:
        prev_price = prices["avg_price_7"]

    if model:
        input_df = _build_model_input(state, commodity, market, prev_price, prices, target_date)
        log_pred = model.predict(input_df)
        predicted_price = float(np.expm1(log_pred[0]))
    else:
        # Fallback if no model is loaded
        predicted_price = prices["avg_price_7"] * 1.05

    avg_price = prices["avg_price_7"]
    demand_score = (predicted_price - avg_price) / avg_price if avg_price else 0.0
    return {
        "predicted_price": round(predicted_price, 2),
        "predicted_price_per_kg": _to_per_kg(predicted_price),
        "month": target_date.month,
        "day": target_date.day,
        "year": target_date.year,
        "avg_price_7": round(float(avg_price), 2),
        "avg_price_7_per_kg": _to_per_kg(avg_price),
        "Min_Price": round(float(prices["Min_Price"]), 2),
        "Min_Price_per_kg": _to_per_kg(prices["Min_Price"]),
        "Max_Price": round(float(prices["Max_Price"]), 2),
        "Max_Price_per_kg": _to_per_kg(prices["Max_Price"]),
        "source": prices["source"],
        "demand_score": round(float(demand_score), 4),
        "demand_label": _demand_label(float(demand_score)),
    }


def _validate_combo(state: str, market: str, commodity: str):
    records = list(_fetch_records_cached(state=state, commodity=commodity))
    if not records:
        return
    market_clean = _normalize_market_name(market)
    if not any(_normalize_market_name(r.get("market", "")) == market_clean for r in records):
        raise HTTPException(
            status_code=400,
            detail=f"Market '{market}' is not valid for commodity '{commodity}' in state '{state}'",
        )


@app.get("/price")
def get_price(state: str, market: str, commodity: str):
    _validate_combo(state, market, commodity)
    prices = _get_prices_with_fallback(commodity, market, state)
    if prices is None:
        raise HTTPException(status_code=404, detail="No price found")
    prices["avg_price_7_per_kg"] = _to_per_kg(prices["avg_price_7"])
    prices["Min_Price_per_kg"] = _to_per_kg(prices["Min_Price"])
    prices["Max_Price_per_kg"] = _to_per_kg(prices["Max_Price"])
    return prices


@app.post("/predict/farmer")
async def predict_farmer(request: FarmerPredictRequest):
    # This aligns the user's robust API with the format the Javascript UI expects
    _validate_combo(request.STATE, request.Market_Name, request.Commodity)
    
    current_pred = _predict_price(
        state=request.STATE,
        commodity=request.Commodity,
        market=request.Market_Name,
        prev_price=request.prev_price,
    )
    
    forecast = []
    for d in [30, 60, 90]:
        target = datetime.today() + timedelta(days=d)
        f_pred = _predict_price(
            state=request.STATE,
            commodity=request.Commodity,
            market=request.Market_Name,
            prev_price=request.prev_price,
            target_date=target
        )
        forecast.append({
            "commodity": request.Commodity,
            "current_price_per_kg": current_pred["avg_price_7_per_kg"],
            "predicted_price_90d_per_kg": f_pred["predicted_price_per_kg"],
            "recommendation": _season_recommendation(f_pred["demand_score"], 0.0)
        })

    return {
        "success": True,
        "current_price_per_kg": current_pred["avg_price_7_per_kg"],
        "predicted_price_per_kg": current_pred["predicted_price_per_kg"],
        "season_forecast": forecast
    }


@app.post("/predict/admin")
async def predict_admin_post(req: AdminPredictRequest):
    # This maps the UI POST request for /predict/admin to the logic below
    cache_key = ("predict_admin", req.state, req.market, 0)
    cached = _get_endpoint_cache(cache_key)
    if cached is not None:
        return cached

    state_records = list(_fetch_records_cached(state=req.state, limit=API_LIMIT_ADMIN))
    # IMPORTANT: make "Crops analyzed" vary by selected market.
    # Previously we used state-level unique commodities, which stays constant per state (e.g., always 102 for Gujarat).
    market_records = state_records
    if req.market:
        market_norm = _normalize_market_name(req.market)
        market_records = [
            r for r in state_records
            if _normalize_market_name(r.get("market", "")) == market_norm
        ]

    commodities = _commodities_from_state_records(market_records)
    # Fallback: if a market has no rows from gov API, fall back to state-level list.
    if not commodities:
        commodities = _commodities_from_state_records(state_records)
    price_index = _build_price_index_for_market(state_records, req.market)
    
    if not commodities:
        return {"success": False}

    target_date = datetime.today()
    results = []
    demand = []
    
    for commodity in commodities:
        try:
            prices = _get_prices_with_bulk_fallback(
                commodity=commodity,
                market=req.market,
                state=req.state,
                state_records=state_records,
                price_index=price_index,
            )
            if prices is None:
                continue
            
            if model:
                input_df = _build_model_input(
                    state=req.state,
                    commodity=commodity,
                    market=req.market,
                    prev_price=prices["avg_price_7"],
                    prices=prices,
                    target_date=target_date,
                )
                log_pred = model.predict(input_df)
                predicted_price = float(np.expm1(log_pred[0]))
            else:
                predicted_price = prices["avg_price_7"] * 1.05
                
            avg_price = prices["avg_price_7"]
            demand_score = (predicted_price - avg_price) / avg_price if avg_price else 0.0
            
            row = {
                "commodity": commodity,
                "predicted_price_per_kg": _to_per_kg(predicted_price),
                "demand_label": _demand_label(float(demand_score)),
                "raw_score": demand_score,
                "current_price": avg_price
            }
            results.append(row)
        except Exception as e:
            print(e)
            continue

    # Sort results by demand score to get top demand crops
    results_sorted = sorted(results, key=lambda x: x["raw_score"], reverse=True)
    
    for r in results_sorted[:20]:
        trend_pct = round(float(r["raw_score"]) * 100.0, 1)
        trend_str = f"Upward {trend_pct}%" if r["raw_score"] > 0 else f"Downward {abs(trend_pct)}%"
        demand.append({
            "commodity": r["commodity"],
            "current_price_per_kg": _to_per_kg(r["current_price"]),
            "trend": trend_str,
            "trend_pct": abs(trend_pct),
            "trend_dir": "up" if r["raw_score"] >= 0 else "down",
            "demand_label": r["demand_label"],
        })

    response = {
        "success": True,
        "all_predictions": results,
        "top_demand": demand
    }
    _set_endpoint_cache(cache_key, response)
    return response


class StateSeasonRequest(BaseModel):
    state: str


@app.get("/api/inventory-intelligence")
def get_inventory_intelligence(limit: int = 15):
    safe_limit = max(1, min(limit, 100))

    if _df.empty:
        demo_rows = []
        demo_crops = [
            "Potato",
            "Tomato",
            "Onion",
            "Banana",
            "Green Chilli",
            "Cabbage",
            "Carrot",
            "Grapes",
        ][:safe_limit]
        for i, crop in enumerate(demo_crops):
            demo_rows.append(
                {
                    "productName": crop,
                    "grade": "A",
                    "quantityAvailable": 1000 + (i * 120),
                    "unit": "kg",
                    "askingPrice": round(18 + (i * 2.3), 2),
                    "storageTemp": round(10 + (i * 0.7), 1),
                    "humidity": round(62 + (i * 1.2), 1),
                    "daysInStorage": 4 + i,
                    "predictedDaysLeft": max(3, 14 - i),
                    "status": "Optimal" if i < 4 else "Low Stock",
                    "createdAt": datetime.today().strftime("%Y-%m-%dT00:00:00"),
                }
            )
        return {
            "success": True,
            "data": demo_rows,
            "source": "demo_fallback",
            "message": "Historical CSV missing. Showing demo inventory rows.",
        }

    source_df = _inventory_df.copy() if not _inventory_df.empty else _df.copy()

    # Price dataset flow (cleaned_data.csv)
    if "Modal_Price" in source_df.columns and "Commodity" in source_df.columns:
        source_df = source_df.dropna(subset=["Commodity", "Modal_Price"])
        if source_df.empty:
            return {"success": False, "message": "No valid rows found in historical data."}

        top_df = source_df.sort_values("Modal_Price", ascending=False).head(safe_limit)
        rows = []
        for _, row in top_df.iterrows():
            modal = float(row.get("Modal_Price", 0.0) or 0.0)
            min_p = float(row.get("Min_Price", modal) or modal)
            max_p = float(row.get("Max_Price", modal) or modal)
            price_volatility = abs(max_p - min_p)

            predicted_days_left = int(np.clip(round(18 - (price_volatility / 120.0)), 2, 20))
            if predicted_days_left <= 4:
                status = "Discount Now"
            elif predicted_days_left <= 8:
                status = "Low Stock"
            else:
                status = "Optimal"

            created_year = int(row.get("year", datetime.today().year) or datetime.today().year)
            created_month = int(row.get("month", datetime.today().month) or datetime.today().month)
            created_day = int(row.get("day", datetime.today().day) or datetime.today().day)

            rows.append(
                {
                    "productName": str(row.get("Commodity", "Unknown")),
                    "grade": str(row.get("Variety", "A")),
                    "quantityAvailable": int(np.clip(round(modal / 2), 100, 5000)),
                    "unit": "kg",
                    "askingPrice": round(_to_per_kg(modal), 2),
                    "storageTemp": round(float(np.clip(12 + (price_volatility / 500.0), 8, 20)), 1),
                    "humidity": round(float(np.clip(62 + (price_volatility / 200.0), 45, 90)), 1),
                    "daysInStorage": int(np.clip(round(6 + (price_volatility / 200.0)), 1, 25)),
                    "predictedDaysLeft": predicted_days_left,
                    "status": status,
                    "createdAt": f"{created_year:04d}-{created_month:02d}-{created_day:02d}T00:00:00",
                }
            )
        return {"success": True, "data": rows, "source": "price_dataset"}

    # Shelf-life dataset flow (crop_shelf_life_500000.csv-style columns)
    if "Produce_Type" in source_df.columns:
        top_df = source_df.head(safe_limit)
        rows = []
        for _, row in top_df.iterrows():
            crop = str(row.get("Produce_Type", "Unknown"))
            temp = float(row.get("Temperature_C", 12.0) or 12.0)
            humidity = float(row.get("Humidity_Percent", 60.0) or 60.0)
            days_in_storage = int(row.get("Days_Since_Harvest", 5) or 5)
            qty = int(row.get("Quantity", 1000) or 1000)
            
            # Predict shelf life using AI model if available
            if shelf_life_model is not None and produce_encoder is not None:
                try:
                    # Transform the crop name to encoded value
                    try:
                        encoded_crop = produce_encoder.transform([crop])[0]
                    except ValueError:
                        # Fallback if the crop wasn't in the training data
                        encoded_crop = 0
                        
                    heat_humidity_index = temp * humidity
                    input_features = pd.DataFrame([[encoded_crop, temp, humidity, days_in_storage, heat_humidity_index]], 
                                                columns=['Produce_Type_Encoded', 'Temperature_C', 'Humidity_Percent', 'Days_Since_Harvest', 'Heat_Humidity_Index'])
                    
                    predicted_days_left = int(round(shelf_life_model.predict(input_features)[0]))
                except Exception as e:
                    print(f"Shelf life model prediction failed: {e}")
                    predicted_days_left = int(np.clip(round(14 - (temp - 10) * 0.8 - (humidity - 60) * 0.03 - (days_in_storage * 0.4)), 1, 20))
            else:
                # Math fallback
                predicted_days_left = int(np.clip(round(14 - (temp - 10) * 0.8 - (humidity - 60) * 0.03 - (days_in_storage * 0.4)), 1, 20))

            if predicted_days_left <= 3:
                status = "Discount Now"
            elif predicted_days_left <= 7:
                status = "Low Stock"
            else:
                status = "Optimal"

            rows.append(
                {
                    "productName": crop,
                    "grade": "A",
                    "quantityAvailable": qty,
                    "unit": "kg",
                    "askingPrice": round(float(row.get("AskingPrice", 0.0) or 0.0), 2),
                    "storageTemp": round(temp, 1),
                    "humidity": round(humidity, 1),
                    "daysInStorage": days_in_storage,
                    "predictedDaysLeft": predicted_days_left,
                    "status": status,
                    "createdAt": datetime.today().strftime("%Y-%m-%dT00:00:00"),
                }
            )
        return {"success": True, "data": rows, "source": "ai_model" if shelf_life_model else "shelf_life_dataset_fallback"}

    return {"success": False, "message": "Unsupported historical data format for inventory intelligence."}


@app.post("/predict/state_season")
async def predict_state_season(req: StateSeasonRequest):
    cache_key = ("state_season", req.state)
    cached = _get_endpoint_cache(cache_key)
    if cached is not None:
        return cached

    state_records = list(_fetch_records_cached(state=req.state, limit=API_LIMIT_ADMIN))
    commodities = set(_commodities_from_state_records(state_records))
    state_price_index = _build_state_price_index(state_records) if state_records else {}
    if not _df.empty and "Commodity" in _df.columns:
        temp_df = _df[_df["STATE"] == req.state]
        commodities.update(temp_df["Commodity"].dropna().unique())

    season_rows = []
    target_date = datetime.today() + timedelta(days=90)
    
    for commodity in list(commodities):
        try:
            prices = _get_state_level_prices_with_fallback(
                commodity,
                req.state,
                state_records=state_records,
                state_price_index=state_price_index,
            )
            if prices is None:
                continue

            current_price = prices["avg_price_7"]
            input_df = _build_model_input(
                state=req.state,
                commodity=commodity,
                market="State Average", # Use a dummy market name
                prev_price=current_price,
                prices=prices,
                target_date=target_date,
            )
            
            if model:
                log_pred = model.predict(input_df)
                pred_price = float(np.expm1(log_pred[0]))
            else:
                pred_price = current_price * 1.05

            growth = (pred_price - current_price) / current_price if current_price else 0.0
            
            season_rows.append({
                "commodity": commodity,
                "current_price_per_kg": _to_per_kg(current_price),
                "predicted_price_90d_per_kg": _to_per_kg(pred_price),
                "growth": growth,
                "recommendation": _season_recommendation(growth, 0.0)
            })
        except Exception as e:
            import traceback
            traceback.print_exc()
            print(f"Error predicting state season for {commodity}: {e}")
            continue

    sorted_rows = sorted(season_rows, key=lambda x: x["growth"], reverse=True)
    response = {
        "success": True,
        "season_forecast": sorted_rows[:20]
    }
    _set_endpoint_cache(cache_key, response)
    return response

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="127.0.0.1", port=8000, reload=True)
