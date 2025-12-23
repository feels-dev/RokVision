from fastapi import APIRouter, HTTPException
from app.schemas.requests import BatchAnalyzeRequest
from app.services.image_processing import ImageProcessor
from app.core.engine import OcrEngine
import logging

router = APIRouter()
logger = logging.getLogger(__name__)

@router.post("/process")
async def process_batch(request: BatchAnalyzeRequest):
    # 1. Decode Full Image once
    full_img = ImageProcessor.base64_to_cv2(request.imageBase64)
    if full_img is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    ocr = OcrEngine.get_instance()
    results = []

    # 2. Loop through requested regions
    # (Note: PaddleOCR batch list processing is faster than loop, 
    # but since we apply different filters per crop, loop is fine here)
    for region in request.regions:
        x, y, w, h = region.box
        
        # Crop & Filter (OpenCV is C++ fast)
        processed_crop = ImageProcessor.process_region(
            full_img, int(x), int(y), int(w), int(h), region.strategy
        )

        if processed_crop is None:
            continue

        # OCR Inference
        ocr_res = ocr.ocr(processed_crop, cls=False, det=False) # det=False aumenta velocidade (confia no crop)

        text = ""
        conf = 0.0
        
        # PaddleOCR returns list of tuples for rec only
        if ocr_res and ocr_res[0]:
            # Formato com det=False é diferente: [('Text', 0.99), ...]
            text_data = ocr_res[0][0] 
            text = text_data[0]
            conf = float(text_data[1])

        results.append({
            "id": region.id,
            "text": text,
            "conf": conf,
            "strategy": region.strategy
        })

    return {"success": True, "results": results}