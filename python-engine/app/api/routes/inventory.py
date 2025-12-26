from fastapi import APIRouter, HTTPException
from app.schemas.requests import OcrRequest
from app.services.image_processing import ImageProcessor
from app.core.engine import OcrEngine
import logging
import numpy as np

router = APIRouter()
logger = logging.getLogger(__name__)

@router.post("/analyze")
async def analyze_inventory_ui(request: OcrRequest):
    """
    Route specialized for User Interfaces (Inventory, Ranking, Chat).
    Optimized for reading small numbers on complex backgrounds.
    """
    img = ImageProcessor.base64_to_cv2(request.imageBase64)
    if img is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    try:
        # 1. Smart Resize (1920px is mandatory for small numbers)
        img_resized, ratio = ImageProcessor.resize_if_needed(img, max_width=1920)

        # 2. CHANGE 1: Sharpen Filter ENABLED
        # This helps highlight the white outline of numbers against the colorful background
        img_final = ImageProcessor.apply_filters(img_resized)
        if img_final is None:
            img_final = img_resized # Fallback if error occurs

        # 3. OCR Engine
        ocr = OcrEngine.get_instance()
        result = ocr.ocr(img_final, cls=False)
        
        blocks = []
        full_text = []

        if result and result[0]:
            h_img, w_img = img_final.shape[:2]

            for line in result[0]:
                text = line[1][0]
                conf = line[1][1]
                
                # 3. CHANGE 2: Drastically reduced minimum confidence
                # Small numbers on icons often have low confidence (e.g., 0.15).
                # Lowered to 0.05 to ensure we capture the data.
                # C# will filter the noise via Regex.
                if conf < 0.05: 
                    continue

                box = line[0] 
                
                # --- COLOR DETECTION ---
                xs = [pt[0] for pt in box]
                ys = [pt[1] for pt in box]
                x_min, x_max = int(min(xs)), int(max(xs))
                y_min, y_max = int(min(ys)), int(max(ys))

                # 25px Padding (Kept per previous adjustment)
                padding = 25 
                y_min_p = max(0, y_min - padding)
                y_max_p = min(h_img, y_max + padding)
                x_min_p = max(0, x_min - padding)
                x_max_p = min(w_img, x_max + padding)

                crop = img_final[y_min_p:y_max_p, x_min_p:x_max_p]
                
                # Detect color in crop (now with sharpen filter applied, which may slightly 
                # alter color, but HSV dominant logic usually holds up)
                color_tag = ImageProcessor.detect_dominant_color(crop)
                # ---------------------

                # Revert coordinates if resized
                if ratio != 1.0:
                     box = [[pt[0]/ratio, pt[1]/ratio] for pt in box]

                blocks.append({
                    "text": text,
                    "box": box,
                    "conf": conf,
                    "color": color_tag
                })
                full_text.append(text)

        return {
            "success": True,
            "full_text": " | ".join(full_text),
            "blocks": blocks
        }

    except Exception as e:
        logger.error(f"Inventory Processing Error: {e}")
        raise HTTPException(status_code=500, detail=str(e))