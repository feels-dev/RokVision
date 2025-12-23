from pydantic import BaseModel
from typing import List

class OcrRequest(BaseModel):
    imageBase64: str

# --- NOVOS MODELOS PARA BATCH ---
class CropRegion(BaseModel):
    id: str             # Identificador (ex: "node_1")
    box: List[int]      # [x, y, w, h]
    strategy: str       # "standard", "binary", "inverted", etc.

class BatchAnalyzeRequest(BaseModel):
    imageBase64: str
    regions: List[CropRegion]