import numpy as np
import pandas as pd


def prepare_training_frame(df: pd.DataFrame, horizon_days: int = 90) -> pd.DataFrame:
    work = df.copy()
    work.columns = work.columns.str.strip().str.replace(" ", "_")

    for col in ["year", "month", "day", "Min_Price", "Max_Price", "avg_price_7", "prev_price", "Modal_Price"]:
        work[col] = pd.to_numeric(work[col], errors="coerce")

    work["date"] = pd.to_datetime(
        dict(year=work["year"].astype("Int64"), month=work["month"].astype("Int64"), day=work["day"].astype("Int64")),
        errors="coerce",
    )
    work = work.dropna(subset=["STATE", "Commodity", "Market_Name", "date", "Modal_Price"])

    group_cols = ["STATE", "Market_Name", "Commodity"]
    work = work.sort_values(group_cols + ["date"])
    work["future_modal_price"] = work.groupby(group_cols)["Modal_Price"].shift(-horizon_days)
    work = work.dropna(subset=["future_modal_price"])

    work["demand_growth"] = (work["future_modal_price"] - work["Modal_Price"]) / work["Modal_Price"].replace(0, np.nan)
    work["demand_growth"] = work["demand_growth"].replace([np.inf, -np.inf], np.nan)
    work = work.dropna(subset=["demand_growth"])
    work["demand_growth"] = work["demand_growth"].clip(-0.9, 3.0)

    work["price_range"] = work["Max_Price"] - work["Min_Price"]
    work["price_center"] = (work["Max_Price"] + work["Min_Price"]) / 2
    work["month_sin"] = np.sin(2 * np.pi * work["month"] / 12.0)
    work["month_cos"] = np.cos(2 * np.pi * work["month"] / 12.0)

    features = [
        "STATE",
        "Commodity",
        "Market_Name",
        "month",
        "day",
        "year",
        "Min_Price",
        "Max_Price",
        "avg_price_7",
        "prev_price",
        "price_range",
        "price_center",
        "month_sin",
        "month_cos",
    ]
    return work[features + ["demand_growth"]].dropna()


def build_inference_row(
    state: str,
    market: str,
    commodity: str,
    target_date: pd.Timestamp,
    min_price: float,
    max_price: float,
    avg_price_7: float,
    prev_price: float,
) -> pd.DataFrame:
    price_range = max_price - min_price
    price_center = (max_price + min_price) / 2
    month_sin = np.sin(2 * np.pi * target_date.month / 12.0)
    month_cos = np.cos(2 * np.pi * target_date.month / 12.0)

    return pd.DataFrame(
        [
            {
                "STATE": state,
                "Commodity": commodity,
                "Market_Name": market,
                "month": int(target_date.month),
                "day": int(target_date.day),
                "year": int(target_date.year),
                "Min_Price": float(min_price),
                "Max_Price": float(max_price),
                "avg_price_7": float(avg_price_7),
                "prev_price": float(prev_price),
                "price_range": float(price_range),
                "price_center": float(price_center),
                "month_sin": float(month_sin),
                "month_cos": float(month_cos),
            }
        ]
    )
