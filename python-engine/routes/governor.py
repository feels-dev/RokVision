from fastapi import APIRouter, HTTPException
from pydantic import BaseModel
from utils.image_utils import base64_to_cv2
from core.engine import ocr_instance

router = APIRouter()

class OcrRequest(BaseModel):
    imageBase64: str

@router.post("/analyze")
async def analyze_governor(request: OcrRequest):
    img = base64_to_cv2(request.imageBase64)
    if img is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    result = ocr_instance.ocr(img, cls=False)
    
    blocks = []
    full_text = []

    if result and result[0]:
        for line in result[0]:
            blocks.append({
                "text": line[1][0],
                "box": line[0],
                "conf": line[1][1]
            })
            full_text.append(line[1][0])

    return {
        "success": True,
        "full_text": "\n".join(full_text),
        "blocks": blocks
    }