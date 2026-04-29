# train_price_model.py

import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.ensemble import RandomForestRegressor
from sklearn.preprocessing import OneHotEncoder
from sklearn.compose import ColumnTransformer
from sklearn.pipeline import Pipeline
from sklearn.metrics import mean_absolute_error, r2_score
import joblib
from pathlib import Path



# ── STEP 1: Load ──────────────────────────────────────────
print("📥 Loading cleaned dataset...")
BASE_DIR = Path(__file__).resolve().parent
file_path = BASE_DIR.parent / "Import Data" / "cleaned_data.csv"
df = pd.read_csv(file_path)
print(f"   Rows: {len(df)} | Columns: {df.columns.tolist()}")

# ── STEP 2: Add extra features from existing columns ──────
df["price_ratio"]     = df["Modal_Price"] / df["price_center"].replace(0, np.nan)
df["price_momentum"]  = df["prev_price"] - df["avg_price_7"]
df["range_ratio"]     = df["price_range"] / df["price_center"].replace(0, np.nan)
df["month_sin"]       = np.sin(2 * np.pi * df["month"] / 12)
df["month_cos"]       = np.cos(2 * np.pi * df["month"] / 12)
df = df.dropna()

# ── STEP 3: Features & Target ─────────────────────────────
cat_cols = ["STATE", "Commodity", "Variety", "Market_Name"]
num_cols = [
    "month", "day", "year",
    "Min_Price", "Max_Price",
    "price_range", "price_center",
    "prev_price", "avg_price_7",
    "price_momentum", "range_ratio",   # new
    "month_sin", "month_cos"           # new — seasonal encoding
]

X = df[cat_cols + num_cols]
y = np.log1p(df["Modal_Price"])

# ── STEP 4: Train/Test Split ──────────────────────────────
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42
)
print(f"   Train: {len(X_train)} | Test: {len(X_test)}")

# ── STEP 5: Preprocessor ──────────────────────────────────
preprocessor = ColumnTransformer([
    ("cat", OneHotEncoder(handle_unknown="ignore", sparse_output=False), cat_cols),
    ("num", "passthrough", num_cols)
])

# ── STEP 6: Tuned Model ───────────────────────────────────
model = RandomForestRegressor(
    n_estimators=500,       # more trees = more stable
    max_depth=30,           # deeper = captures more patterns
    min_samples_leaf=1,     # allow finer splits
    max_features=0.4,       # try more features per split
    n_jobs=-1,
    random_state=42
)

# ── STEP 7: Pipeline ──────────────────────────────────────
pipeline = Pipeline([
    ("preprocessor", preprocessor),
    ("model",        model)
])

# ── STEP 8: Train ─────────────────────────────────────────
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42
)

print("🚀 Training model... (may take 4-6 mins)")
pipeline.fit(X_train, y_train)
print("✅ Training complete!")

# ── STEP 9: Evaluate ──────────────────────────────────────
y_pred   = np.expm1(pipeline.predict(X_test))
y_actual = np.expm1(y_test)

mae = mean_absolute_error(y_actual, y_pred)
r2  = r2_score(y_actual, y_pred)

print(f"\n📊 Results:")
print(f"   MAE : {mae:.2f}")
print(f"   R²  : {r2:.4f}")

# ── STEP 10: Save if improved ─────────────────────────────
if mae < 28.13:
    joblib.dump(pipeline, "price_model.pkl")
    print(f"\n💾 Saved → price_model.pkl (improved from 28.13 → {mae:.2f})")
else:
    print(f"\n⚠️ Not saved — MAE {mae:.2f} is worse than current 28.13")

# ── STEP 11: Sample Predictions ───────────────────────────
print("\n🔍 Sample Predictions vs Actual:")
sample = pd.DataFrame({
    "Actual":    y_actual.values[:10],
    "Predicted": y_pred[:10]
})  
sample["Difference"] = abs(sample["Actual"] - sample["Predicted"])
print(sample.to_string(index=False))