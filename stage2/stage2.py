# Re-import necessary modules due to code execution state reset
import pandas as pd
from datetime import time

# Define time filtering parameters
start_dt = pd.Timestamp("2025-06-02T00:00:00", tz="Europe/Amsterdam")
end_dt   = pd.Timestamp("2025-06-09T00:00:00", tz="Europe/Amsterdam")
start_t  = time(6, 0)
end_t    = time(22, 0)

# Load the newly uploaded CSV file
w2m_file_path = 'hippo1.csv'
w2m_df = pd.read_csv(w2m_file_path)
w2m_df['Slot'] = pd.to_datetime(w2m_df['Slot'])

# Filter by date and time range
filtered_w2m = w2m_df[
    (w2m_df['Slot'] >= start_dt) &
    (w2m_df['Slot'] < end_dt) &
    (w2m_df['Slot'].dt.time >= start_t) &
    (w2m_df['Slot'].dt.time < end_t)
    ].copy()

# Identify people columns
people_cols_w2m = [col for col in filtered_w2m.columns if col != 'Slot']

# Add CountAvailable and IsViable
filtered_w2m['CountAvailable'] = filtered_w2m[people_cols_w2m].sum(axis=1)
filtered_w2m['IsViable'] = filtered_w2m['CountAvailable'] >= 3

# Add IsLongEnough based on runs of viable slots
filtered_w2m['RunID'] = (filtered_w2m['IsViable'] != filtered_w2m['IsViable'].shift()).cumsum()
run_lengths_w2m = filtered_w2m.groupby('RunID')['IsViable'].transform('sum')
filtered_w2m['IsLongEnough'] = (filtered_w2m['IsViable']) & (run_lengths_w2m >= 5)
filtered_w2m.drop(columns=['RunID'], inplace=True)

# Export processed overview
output_path_w2m = 'hippo2.csv'
filtered_w2m.to_csv(output_path_w2m, index=False)

# Preview the result
filtered_w2m.head(10)
