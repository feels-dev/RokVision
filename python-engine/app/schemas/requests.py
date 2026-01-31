# Path: app/schemas/requests.py
from pydantic import BaseModel
from typing import List

class OcrRequest(BaseModel):
    imageBase64: str

# --- NEW BATCH MODELS ---
class CropRegion(BaseModel):
    id: str             # Identifier (e.g., "node_1")
    box: List[int]      # [x, y, w, h]
    strategy: str       # "standard", "binary", "inverted", "WhiteIsolation", etc.

class BatchAnalyzeRequest(BaseModel):
    imageBase64: str
    regions: List[CropRegion]