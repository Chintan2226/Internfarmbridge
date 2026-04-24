# import pandas as pd
# from sklearn.model_selection import train_test_split
# from sklearn.ensemble import RandomForestRegressor
# from sklearn.preprocessing import LabelEncoder
# import joblib

# # 1. Load the dummy dataset
# # df = pd.read_csv("produce_data.csv")
# # Use ../ to go up one folder level
# df = pd.read_csv("../Import Data/product_data.csv")

# # 2. Encode the text column (Produce_Type) to numbers
# le = LabelEncoder()
# df['Produce_Type_Encoded'] = le.fit_transform(df['Produce_Type'])

# # 3. Select Features (X) and Target (y)
# X = df[['Produce_Type_Encoded', 'Temperature_C', 'Humidity_Percent', 'Days_Since_Harvest']]
# y = df['Remaining_Shelf_Life_Days']

# # 4. Split data and Train the Regression Model
# X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
# model = RandomForestRegressor(n_estimators=100)
# model.fit(X_train, y_train)

# # 5. Save the model and encoder
# joblib.dump(model, "shelf_life_model.pkl")
# joblib.dump(le, "produce_encoder.pkl")

# print("✅ Shelf Life Model trained and saved!")



# import pandas as pd
# from sklearn.model_selection import train_test_split
# from sklearn.ensemble import RandomForestRegressor
# from sklearn.preprocessing import LabelEncoder
# import joblib

# # 1. Load the new fruits dataset
# df = pd.read_csv("../Import Data/fruits_dataset.csv")

# # 2. Data Cleaning
# df = df.dropna()

# # 3. Define the exact columns from your dataset
# target_column = 'shelf_life_days'  # What we want to predict
# text_features = ['fruit_name', 'season', 'taste_profile'] # Text columns
# numeric_features = ['sugar_g', 'acidity_pH', 'water_percent'] # Number columns

# # 4. Encode the text columns into numbers
# le_fruit = LabelEncoder()
# le_season = LabelEncoder()
# le_taste = LabelEncoder()

# df['fruit_encoded'] = le_fruit.fit_transform(df['fruit_name'])
# df['season_encoded'] = le_season.fit_transform(df['season'])
# df['taste_encoded'] = le_taste.fit_transform(df['taste_profile'])

# # 5. Assemble final features (X) and target (y)
# feature_columns = ['fruit_encoded', 'season_encoded', 'taste_encoded'] + numeric_features
# X = df[feature_columns]
# y = df[target_column]

# # 6. Split and Train
# X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
# model = RandomForestRegressor(n_estimators=100)
# model.fit(X_train, y_train)

# # 7. Save the model and ALL encoders
# joblib.dump(model, "shelf_life_model.pkl")
# joblib.dump(le_fruit, "fruit_encoder.pkl")
# joblib.dump(le_season, "season_encoder.pkl")
# joblib.dump(le_taste, "taste_encoder.pkl")

# print("✅ Fruit Shelf Life Model trained and saved!")


# import pandas as pd
# from sklearn.model_selection import train_test_split
# from sklearn.ensemble import RandomForestRegressor
# from sklearn.preprocessing import LabelEncoder
# import joblib

# # 1. Load the massive 500k row dataset
# df = pd.read_csv("../Import Data/crop_shelf_life_500000.csv")

# # 2. Drop any accidental blank rows (critical for big data)
# df = df.dropna()

# # 3. Setup the Encoder for the Produce_Type text
# le = LabelEncoder()
# df['Produce_Type_Encoded'] = le.fit_transform(df['Produce_Type'])

# # 4. Map the exact columns from your new dataset
# X = df[['Produce_Type_Encoded', 'Temperature_C', 'Humidity_Percent', 'Days_Since_Harvest']]
# y = df['Remaining_Shelf_Life_Days']

# # 5. Split and Train
# # Using n_jobs=-1 tells your computer to use ALL of its processing power
# X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
# model = RandomForestRegressor(n_estimators=100, n_jobs=-1) 

# print("Training model on 500,000 rows... Please wait.")
# model.fit(X_train, y_train)

# # 6. Save the model and the encoder
# joblib.dump(model, "shelf_life_model.pkl")
# joblib.dump(le, "produce_encoder.pkl")

# print("✅ Massive Model trained and saved successfully!")


# #working last
# import pandas as pd
# from sklearn.model_selection import train_test_split
# from sklearn.ensemble import RandomForestRegressor
# from sklearn.preprocessing import LabelEncoder
# from sklearn.metrics import mean_absolute_error, r2_score
# import joblib

# print("⏳ Loading massive 500,000 row dataset...")
# # 1. Load the dataset (This creates the 'df' variable!)
# df = pd.read_csv("../Import Data/crop_shelf_life_500000.csv")

# # 2. Clean the data
# df = df.dropna()

# # 3. Encode the text column
# le = LabelEncoder()
# df['Produce_Type_Encoded'] = le.fit_transform(df['Produce_Type'])

# # 4. Map the columns
# X = df[['Produce_Type_Encoded', 'Temperature_C', 'Humidity_Percent', 'Days_Since_Harvest']]
# y = df['Remaining_Shelf_Life_Days']

# # 5. Split the data
# X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

# # 6. Train the model
# print("🧠 Training model on 400,000 rows (using 100,000 for testing)... Please wait.")
# model = RandomForestRegressor(n_estimators=100, n_jobs=-1) 
# model.fit(X_train, y_train)

# # 7. Check the Accuracy
# print("\n🔍 Evaluating Model Performance...")
# predictions = model.predict(X_test)
# mae = mean_absolute_error(y_test, predictions)
# r2 = r2_score(y_test, predictions)

# print(f"📉 Mean Absolute Error: The AI is off by an average of {round(mae, 2)} days.")
# # Multiply the r2 score by 100 to get the percentage
# accuracy_percentage = r2 * 100
# print(f"📈 Model Accuracy: {round(accuracy_percentage, 2)}% (Out of 100%)")
# print("-" * 40)

# # 8. Save the model
# joblib.dump(model, "shelf_life_model.pkl")
# joblib.dump(le, "produce_encoder.pkl")

# print("✅ Massive Model trained and saved successfully!")


#after adding new column

import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.ensemble import RandomForestRegressor
from sklearn.preprocessing import LabelEncoder
from sklearn.metrics import mean_absolute_error, r2_score
import joblib

print("⏳ Loading massive row dataset...")
# 1. Load the dataset
df = pd.read_csv("../Import Data/crop_shelf_life_500000.csv")

# 2. Clean the data AND Remove Outliers
df = df.dropna()
df = df[(df['Humidity_Percent'] >= 0) & (df['Humidity_Percent'] <= 100)]
df = df[(df['Temperature_C'] >= -10) & (df['Temperature_C'] <= 40)]

# 3. Encode the text column
le = LabelEncoder()
df['Produce_Type_Encoded'] = le.fit_transform(df['Produce_Type'])

# 4. Feature Engineering: Create the new smart column
df['Heat_Humidity_Index'] = df['Temperature_C'] * df['Humidity_Percent']

# 5. Map the columns (Now expecting 5 inputs!)
X = df[['Produce_Type_Encoded', 'Temperature_C', 'Humidity_Percent', 'Days_Since_Harvest', 'Heat_Humidity_Index']]
y = df['Remaining_Shelf_Life_Days']

# 6. Split the data
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

# 7. Train the optimized model
print("🧠 Training optimized model... Please wait.")
model = RandomForestRegressor(
    n_estimators=200,       
    max_depth=25,           
    min_samples_split=10,   
    n_jobs=-1
) 
model.fit(X_train, y_train)

# 8. Check the Accuracy
print("\n🔍 Evaluating Model Performance...")
predictions = model.predict(X_test)
mae = mean_absolute_error(y_test, predictions)
r2 = r2_score(y_test, predictions)

print(f"📉 Mean Absolute Error: The AI is off by an average of {round(mae, 2)} days.")
print(f"📈 Model Accuracy: {round(r2 * 100, 2)}% (Out of 100%)")
print("-" * 40)

# 9. Save the upgraded model and encoder
joblib.dump(model, "shelf_life_model.pkl")
joblib.dump(le, "produce_encoder.pkl")

print("✅ Optimized Model trained and saved successfully!")