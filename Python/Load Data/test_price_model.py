import joblib
import numpy as np
import pandas as pd

# ── Load Model ────────────────────────────────────────────
model = joblib.load("price_model.pkl")

# ── Sample Input ──────────────────────────────────────────
sample = pd.DataFrame([{
    "STATE":        "Gujarat",
    "Commodity":    "Tomato",
    "Variety":      "Local",
    "Market_Name":  "Surat",
    "month":        3,
    "day":          20,
    "year":         2024,
    "Min_Price":    500,
    "Max_Price":    800,
    "price_range":  300,        # Max_Price - Min_Price
    "price_center": 650,        # (Max_Price + Min_Price) / 2
    "prev_price":   620,        # yesterday's modal price (estimate)
    "avg_price_7":  610         # 7-day average price (estimate)
}])

# ── Predict ───────────────────────────────────────────────
log_prediction = model.predict(sample)
predicted_price = np.expm1(log_prediction[0])  # reverse log transform

print(f"✅ Predicted Modal Price: ₹{predicted_price:.2f}")