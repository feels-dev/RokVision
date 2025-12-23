from fastapi import APIRouter, HTTPException
import uuid
import os
import cv2
import logging
from concurrent.futures import ThreadPoolExecutor

from app.schemas.requests import OcrRequest
from app.services.image_processing import ImageProcessor
from app.core.engine import OcrEngine

router = APIRouter()
logger = logging.getLogger(__name__)

@router.post("/analyze")
async def analyze_report(request: OcrRequest):
    # 1. Decode
    img_raw = ImageProcessor.base64_to_cv2(request.imageBase64)
    if img_raw is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    try:
        # 2. Resize (Gain ~300ms on 4K images)
        # 1920px is enough for RoK text.
        img_resized, scale_ratio = ImageProcessor.resize_if_needed(img_raw, max_width=1920)

        # 3. Process Container (Isolate Paper)
        processed_img, is_isolated = ImageProcessor.isolate_paper(img_resized)

        # 4. Filters (Sharpen)
        final_img = ImageProcessor.apply_filters(processed_img)

        # 5. Save to Disk (Optimization: JPG is faster to write than PNG, usually)
        # Keeping PNG for precision as per requirement, but consider JPG quality=95 for speed.
        filename = f"proc_{uuid.uuid4().hex}.png"
        
        # Path fixed for Docker volume consistency
        save_path = os.path.join("/app/wwwroot/uploads", filename)
        
        # Write to disk
        cv2.imwrite(save_path, final_img)

        # 6. OCR Execution
        ocr = OcrEngine.get_instance()
        result = ocr.ocr(final_img, cls=False)
        
        blocks = []
        if result and result[0]:
            for line in result[0]:
                conf = float(line[1][1])
                if conf > 0.10: # Filter garbage
                    # Note: We don't need to upscale coordinates back if C# consumes the saved processed image directly!
                    # The saved image matches these coordinates.
                    blocks.append({
                        "text": str(line[1][0]),
                        "conf": conf,
                        "box": line[0] 
                    })

        return {
            "success": True,
            "processed_image_path": filename,
            "container": {
                "is_isolated": is_isolated,
                "canvas_size": {
                    "width": int(final_img.shape[1]),
                    "height": int(final_img.shape[0])
                }
            },
            "blocks": blocks
        }

    except Exception as e:
        logger.error(f"🔥 Report Processing Error: {e}")
        raise HTTPException(status_code=500, detail=str(e))