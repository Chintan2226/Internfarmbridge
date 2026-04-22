# #changing code with new column

# from fastapi import FastAPI
# import joblib
# import uvicorn
# import numpy as np

# app = FastAPI()

# # 1. Load the upgraded model and encoder into memory
# print("Loading AI Model...")
# model = joblib.load("shelf_life_model.pkl")
# encoder = joblib.load("produce_encoder.pkl")
# print("AI Model Loaded Successfully!")

# @app.get("/")
# def read_root():
#     return {"message": "Farmer-To-Vendor ML API is running!"}

# # 2. The Prediction Endpoint
# @app.post("/predict-shelf-life")
# async def predict_shelf_life(produce_type: str, temperature: float, humidity: float, days_since_harvest: int):
#     try:
#         # Check if the AI knows this crop
#         if produce_type not in encoder.classes_:
#             return {"error": f"Crop '{produce_type}' not recognized by the AI."}

#         # Convert text to number
#         encoded_type = encoder.transform([produce_type])[0]
        
#         # 👉 Calculate the engineered feature on the fly
#         heat_humidity_idx = temperature * humidity
        
#         # Prepare the exact 5 variables the model expects, in order
#         input_data = np.array([[encoded_type, temperature, humidity, days_since_harvest, heat_humidity_idx]])
        
#         # Make the prediction
#         prediction = model.predict(input_data)
        
#         # Return the result to the C# application
#         return {"predicted_remaining_days": round(prediction[0], 2)}
        
#     except Exception as e:
#         return {"error": str(e)}

# # Run the server automatically if this script is executed
# if __name__ == "__main__":
#     uvicorn.run("ml_api1:app", host="127.0.0.1", port=8001, reload=True)



from fastapi import FastAPI
import joblib
import uvicorn
import numpy as np
import pandas as pd
import os

app = FastAPI()

# 1. Load the upgraded model and encoder into memory
print("Loading AI Model...")
model = joblib.load("shelf_life_model.pkl")
encoder = joblib.load("produce_encoder.pkl")
print("AI Model Loaded Successfully!")

@app.get("/")
def read_root():
    return {"message": "Farmer-To-Vendor ML API is running!"}

# ---------------------------------------------------------
# ENDPOINT 1: Single Prediction (Good for testing/calculators)
# ---------------------------------------------------------
@app.post("/predict-shelf-life")
async def predict_shelf_life(produce_type: str, temperature: float, humidity: float, days_since_harvest: int):
    try:
        if produce_type not in encoder.classes_:
            return {"error": f"Crop '{produce_type}' not recognized by the AI."}

        encoded_type = encoder.transform([produce_type])[0]
        heat_humidity_idx = temperature * humidity
        
        input_data = np.array([[encoded_type, temperature, humidity, days_since_harvest, heat_humidity_idx]])
        prediction = model.predict(input_data)
        
        return {"predicted_remaining_days": round(prediction[0], 2)}
    except Exception as e:
        return {"error": str(e)}

# ---------------------------------------------------------
# ENDPOINT 2: Bulk Prediction for the Admin Dashboard
# ---------------------------------------------------------
# ---------------------------------------------------------
# ENDPOINT 2: Bulk Prediction for the Admin Dashboard
# ---------------------------------------------------------
@app.get("/api/inventory-intelligence")
async def get_inventory_intelligence():
    try:
        # 1. Get the absolute path
        BASE_DIR = os.path.dirname(os.path.abspath(__file__))
        
        # FIX 1: Point to the actual CSV file that exists in your folder
        csv_path = os.path.abspath(os.path.join(BASE_DIR, '..', 'Import Data', 'crop_shelf_life_500000.csv'))
        
        if not os.path.exists(csv_path):
            return {"success": False, "message": f"CSV not found at exactly: {csv_path}"}
            
        df = pd.read_csv(csv_path)
        
        # Take the top 15 rows to keep the dashboard lightning fast
        df = df.head(15)

        results = []
        for index, row in df.iterrows():
            # FIX 2: Use the exact column names from your crop_shelf_life_500000.csv
            produce_type = str(row.get('Produce_Type', 'Potato'))
            temp = float(row.get('Temperature_C', 12.0))
            humidity = float(row.get('Humidity_Percent', 60.0))
            days_in_storage = int(row.get('Days_Since_Harvest', 5))
            
            # 3. AI Prediction Logic
            status = "Optimal"
            predicted_days = 0
            
            if produce_type in encoder.classes_:
                encoded_type = encoder.transform([produce_type])[0]
                heat_humidity_idx = temp * humidity
                
                input_data = np.array([[encoded_type, temp, humidity, days_in_storage, heat_humidity_idx]])
                predicted_days = int(model.predict(input_data)[0])
                
                # Set Status based on the AI's prediction
                if predicted_days <= 3:
                    status = "Discount Now"
                elif predicted_days <= 7:
                    status = "Low Stock" 
            else:
                status = "Unknown Crop"

            # 4. Build the JSON object for C#
            results.append({
                "productName": produce_type,
                "grade": "A", 
                "quantityAvailable": int(row.get('Quantity', 1000)), # Defaulting to 1000 since it's not in the new CSV
                "unit": "kg",
                "askingPrice": 0, 
                "storageTemp": temp,
                "humidity": humidity,
                "daysInStorage": days_in_storage,
                "predictedDaysLeft": predicted_days,
                "status": status,
                "createdAt": "2026-04-20T00:00:00" 
            })

        return {"success": True, "data": results}

    except Exception as e:
        return {"success": False, "message": str(e)}