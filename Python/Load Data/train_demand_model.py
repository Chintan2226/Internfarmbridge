from pathlib import Path

import joblib
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import mean_absolute_error, r2_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder

from demand_features import prepare_training_frame

def main():
    base_dir = Path(__file__).resolve().parent
    data_path = base_dir.parent / "Import Data" / "cleaned_data.csv"
    model_path = base_dir / "demand_model.pkl"

    df = pd.read_csv(data_path)
    train_df = prepare_training_frame(df, horizon_days=90)
    if train_df.empty:
        raise RuntimeError("No rows available for demand model training.")

    y = train_df["demand_growth"]
    x = train_df.drop(columns=["demand_growth"])

    categorical = ["STATE", "Commodity", "Market_Name"]
    numeric = [c for c in x.columns if c not in categorical]

    preprocessor = ColumnTransformer(
        transformers=[
            ("cat", OneHotEncoder(handle_unknown="ignore"), categorical),
            ("num", "passthrough", numeric),
        ]
    )

    model = Pipeline(
        steps=[
            ("prep", preprocessor),
            ("reg", RandomForestRegressor(n_estimators=250, random_state=42, n_jobs=-1)),
        ]
    )

    x_train, x_test, y_train, y_test = train_test_split(x, y, test_size=0.2, random_state=42)
    model.fit(x_train, y_train)
    preds = model.predict(x_test)

    print(f"Train rows: {len(x_train)} | Test rows: {len(x_test)}")
    print(f"MAE: {mean_absolute_error(y_test, preds):.4f}")
    print(f"R2: {r2_score(y_test, preds):.4f}")

    joblib.dump(model, model_path)
    print(f"Saved demand model to: {model_path}")

if __name__ == "__main__":
    main()
