from fastapi import APIRouter, HTTPException
from pydantic import BaseModel
import cv2
import numpy as np
import uuid
import os
import logging
from utils.image_utils import base64_to_cv2
from core.engine import ocr_instance

# Configure logger for this module
logger = logging.getLogger(__name__)

router = APIRouter()

class ReportRequest(BaseModel):
    imageBase64: str

class ContainerProcessor:
    @staticmethod
    def process(img):
        """
        Attempts to isolate the beige paper. If it fails, applies a safety central crop.
        """
        h_orig, w_orig = img.shape[:2]
        hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
        
        # Optimized Beige Range
        lower_beige = np.array([5, 15, 90])
        upper_beige = np.array([40, 180, 255])
        
        mask = cv2.inRange(hsv, lower_beige, upper_beige)
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (25, 25))
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        
        if contours:
            cnt = max(contours, key=cv2.contourArea)
            if cv2.contourArea(cnt) > (h_orig * w_orig * 0.15):
                rect_min = cv2.minAreaRect(cnt)
                box_pts = np.array(cv2.boxPoints(rect_min), dtype="float32")
                
                rect = np.zeros((4, 2), dtype="float32")
                s = box_pts.sum(axis=1)
                rect[0] = box_pts[np.argmin(s)]
                rect[2] = box_pts[np.argmax(s)]
                diff = np.diff(box_pts, axis=1)
                rect[1] = box_pts[np.argmin(diff)]
                rect[3] = box_pts[np.argmax(diff)]

                (tl, tr, br, bl) = rect
                widthA = np.sqrt(((br[0] - bl[0]) ** 2) + ((br[1] - bl[1]) ** 2))
                widthB = np.sqrt(((tr[0] - tl[0]) ** 2) + ((tr[1] - tl[1]) ** 2))
                heightA = np.sqrt(((tr[0] - br[0]) ** 2) + ((tr[1] - br[1]) ** 2))
                heightB = np.sqrt(((tl[0] - bl[0]) ** 2) + ((tl[1] - bl[1]) ** 2))
                
                maxWidth, maxHeight = int(max(widthA, widthB)), int(max(heightA, heightB))
                dst = np.array([[0, 0], [maxWidth-1, 0], [maxWidth-1, maxHeight-1], [0, maxHeight-1]], dtype="float32")
                
                M = cv2.getPerspectiveTransform(rect, dst)
                warped = cv2.warpPerspective(img, M, (maxWidth, maxHeight))
                return warped, M, rect

        # FALLBACK: Safe crop
        y1, y2 = int(h_orig * 0.12), int(h_orig * 0.88)
        x1, x2 = int(w_orig * 0.15), int(w_orig * 0.85)
        return img[y1:y2, x1:x2], None, None

@router.post("/analyze")
async def analyze_report(request: ReportRequest):
    img_raw = base64_to_cv2(request.imageBase64)
    if img_raw is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    try:
        processed_img, transform_matrix, _ = ContainerProcessor.process(img_raw)
        is_isolated = transform_matrix is not None
        
        # Sharpen filter
        sharpen_kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
        processed_img = cv2.filter2D(processed_img, -1, sharpen_kernel)
        
        # SAVE PROCESSED IMAGE (Crucial for the C# Magnifier)
        # We use UUID to avoid collision during simultaneous accesses
        filename = f"proc_{uuid.uuid4().hex}.png"
        save_path = os.path.join("/app/wwwroot/uploads", filename)
        cv2.imwrite(save_path, processed_img)

        # OCR on processed image
        result = ocr_instance.ocr(processed_img, cls=False)
        
        blocks = []
        if result and result[0]:
            for line in result[0]:
                conf = float(line[1][1])
                # Low threshold (0.10) for C# to decide what is useful
                if conf > 0.10:
                    blocks.append({
                        "text": str(line[1][0]),
                        "conf": conf,
                        "box": [[float(c) for c in pt] for pt in line[0]]
                    })

        return {
            "success": True,
            "processed_image_path": filename, # C# will use this file for the Magnifier
            "container": {
                "is_isolated": is_isolated,
                "canvas_size": {
                    "width": int(processed_img.shape[1]),
                    "height": int(processed_img.shape[0])
                }
            },
            "blocks": blocks
        }
    except Exception as e:
        logger.error(f"🔥 Python Error: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))