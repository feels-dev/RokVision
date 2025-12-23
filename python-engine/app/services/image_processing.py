import cv2
import numpy as np
import base64
import logging

# Configure logger for this module
logger = logging.getLogger(__name__)

class ImageProcessor:
    
    @staticmethod
    def base64_to_cv2(b64_str: str):
        """Converts Base64 string to OpenCV format (BGR)."""
        try:
            # Remove header if present (e.g., "data:image/png;base64,")
            if "," in b64_str:
                b64_str = b64_str.split(",")[1]
                
            img_bytes = base64.b64decode(b64_str)
            nparr = np.frombuffer(img_bytes, np.uint8)
            img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            return img
        except Exception as e:
            logger.error(f"❌ Error decoding base64: {e}")
            return None

    @staticmethod
    def resize_if_needed(img, max_width=1920):
        """
        Resizes the image if it exceeds max_width while maintaining aspect ratio.
        Returns the resized image and the scale ratio used.
        """
        h, w = img.shape[:2]
        if w > max_width:
            ratio = max_width / float(w)
            new_h = int(h * ratio)
            # INTER_AREA is best for downscaling (preserves text sharpness)
            resized = cv2.resize(img, (max_width, new_h), interpolation=cv2.INTER_AREA)
            return resized, ratio
        return img, 1.0

    @staticmethod
    def isolate_paper(img):
        """
        Attempts to isolate the beige paper from the background.
        Uses HSV thresholding and Perspective Transform.
        Falls back to a central crop if detection fails.
        """
        h_orig, w_orig = img.shape[:2]
        
        # Convert to HSV color space
        hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
        
        # Optimized Beige Range for RoK
        lower_beige = np.array([5, 15, 90])
        upper_beige = np.array([40, 180, 255])
        
        mask = cv2.inRange(hsv, lower_beige, upper_beige)
        
        # Morphological operations to remove noise
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (25, 25))
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        
        # Find contours
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        
        if contours:
            # Find the largest contour
            cnt = max(contours, key=cv2.contourArea)
            
            # Check if the area is significant (at least 15% of the screen)
            if cv2.contourArea(cnt) > (h_orig * w_orig * 0.15):
                rect_min = cv2.minAreaRect(cnt)
                box_pts = np.array(cv2.boxPoints(rect_min), dtype="float32")
                
                # Order points: top-left, top-right, bottom-right, bottom-left
                rect = np.zeros((4, 2), dtype="float32")
                s = box_pts.sum(axis=1)
                rect[0] = box_pts[np.argmin(s)]
                rect[2] = box_pts[np.argmax(s)]
                diff = np.diff(box_pts, axis=1)
                rect[1] = box_pts[np.argmin(diff)]
                rect[3] = box_pts[np.argmax(diff)]

                (tl, tr, br, bl) = rect
                
                # Calculate width and height of the new image
                widthA = np.sqrt(((br[0] - bl[0]) ** 2) + ((br[1] - bl[1]) ** 2))
                widthB = np.sqrt(((tr[0] - tl[0]) ** 2) + ((tr[1] - tl[1]) ** 2))
                heightA = np.sqrt(((tr[0] - br[0]) ** 2) + ((tr[1] - br[1]) ** 2))
                heightB = np.sqrt(((tl[0] - bl[0]) ** 2) + ((tl[1] - bl[1]) ** 2))
                
                maxWidth = int(max(widthA, widthB))
                maxHeight = int(max(heightA, heightB))
                
                # Destination coordinates
                dst = np.array([
                    [0, 0], 
                    [maxWidth-1, 0], 
                    [maxWidth-1, maxHeight-1], 
                    [0, maxHeight-1]
                ], dtype="float32")
                
                # Apply Perspective Warp
                M = cv2.getPerspectiveTransform(rect, dst)
                warped = cv2.warpPerspective(img, M, (maxWidth, maxHeight))
                
                # Return result and Success Flag
                return warped, True

        # FALLBACK: Safe Central Crop
        # If isolation fails, we assume the report is centered
        y1, y2 = int(h_orig * 0.12), int(h_orig * 0.88)
        x1, x2 = int(w_orig * 0.15), int(w_orig * 0.85)
        
        # Ensure indices are valid
        y1, y2 = max(0, y1), min(h_orig, y2)
        x1, x2 = max(0, x1), min(w_orig, x2)
        
        return img[y1:y2, x1:x2], False

    @staticmethod
    def apply_filters(img):
        """Applies a sharpening filter to enhance text edges."""
        sharpen_kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
        return cv2.filter2D(img, -1, sharpen_kernel)

    @staticmethod
    def process_region(full_img, x, y, w, h, strategy="default"):
        """
        Crops a specific region and applies filters based on strategy.
        Used for Batch Processing / Magnifier logic.
        """
        ih, iw = full_img.shape[:2]
        
        # Safety bounds check
        x, y = max(0, x), max(0, y)
        w, h = min(w, iw - x), min(h, ih - y)
        
        if w <= 0 or h <= 0:
            return None
        
        # Crop
        crop = full_img[y:y+h, x:x+w]

        # Upscale: Helps OCR read small/blurry numbers
        # Bicubic interpolation is good for enlarging
        crop = cv2.resize(crop, None, fx=2.5, fy=2.5, interpolation=cv2.INTER_CUBIC)

        # Strategies
        if strategy == "HighContrastBinary":
            gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
            # Binary Threshold
            _, binary = cv2.threshold(gray, 150, 255, cv2.THRESH_BINARY)
            return binary

        elif strategy == "InvertedBinary":
            gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
            # Invert colors (White text on black background) -> Black text on white
            gray = cv2.bitwise_not(gray)
            _, binary = cv2.threshold(gray, 120, 255, cv2.THRESH_BINARY)
            return binary

        elif strategy == "Sharpen":
            kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
            return cv2.filter2D(crop, -1, kernel)

        # Default: Just Grayscale
        return cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)