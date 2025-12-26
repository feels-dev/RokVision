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
            logger.error(f"Error decoding base64: {e}")
            return None

    @staticmethod
    def resize_if_needed(img, max_width=1920):
        """
        Resizes the image if it exceeds max_width while maintaining aspect ratio.
        Returns the resized image and the scale ratio used.
        """
        if img is None:
            return None, 1.0

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
        Returns: (processed_image, is_isolated_boolean)
        """
        if img is None:
            return None, False

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
        if img is None: return None
        sharpen_kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
        return cv2.filter2D(img, -1, sharpen_kernel)

    @staticmethod
    def process_region(full_img, x, y, w, h, strategy="default"):
        """
        Crops a specific region and applies filters based on strategy.
        Used for Batch Processing / Magnifier logic.
        """
        if full_img is None: return None
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
        crop = cv2.resize(crop, None, fx=3.0, fy=3.0, interpolation=cv2.INTER_CUBIC)

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

        # --- STRATEGY: WHITE ISOLATION ---
        elif strategy == "WhiteIsolation":
            # Convert to HLS (Hue, Lightness, Saturation)
            # The L (Lightness) channel is perfect for finding pure white regardless of background color.
            hls = cv2.cvtColor(crop, cv2.COLOR_BGR2HLS)
            
            # Define "White" range
            # L > 180 (0-255) catches bright whites and light greys (shiny numbers)
            lower_white = np.array([0, 180, 0])
            upper_white = np.array([255, 255, 255])
            
            # Create mask (White becomes 255, everything else 0)
            mask = cv2.inRange(hls, lower_white, upper_white)
            
            # Noise cleaning (removes isolated white dots)
            kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (2, 2))
            mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
            
            # Slight dilation to "thicken" thin numbers like "1"
            # This helps the OCR not lose fine strokes
            mask = cv2.dilate(mask, kernel, iterations=1)
            
            # Invert to Black on White (OCR prefers black text on white background)
            final = cv2.bitwise_not(mask)
            
            return final

        elif strategy == "Sharpen":
            kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
            return cv2.filter2D(crop, -1, kernel)

        # Default: Just Grayscale
        return cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)

    @staticmethod
    def detect_dominant_color(crop_img):
        """
        Analyzes an image crop and returns the dominant color based on HSV ranges.
        Targeted for Rise of Kingdoms item rarities (Green, Blue, Purple, Gold).
        Returns: 'Green', 'Blue', 'Purple', 'Gold', 'Red' or 'Unknown'.
        """
        if crop_img is None or crop_img.size == 0:
            return "Unknown"

        # 1. Convert to HSV (Hue, Saturation, Value)
        hsv = cv2.cvtColor(crop_img, cv2.COLOR_BGR2HSV)
        
        # 2. Define color ranges (Adjusted for RoK artistic style)
        # Note: OpenCV uses Hue 0-179.
        
        colors = {
            "Red": [
                (np.array([0, 70, 50]), np.array([10, 255, 255])),
                (np.array([170, 70, 50]), np.array([180, 255, 255])) # Red wraps around
            ],
            "Gold": [ # Legendary Books / Generic Speedups
                (np.array([15, 70, 70]), np.array([35, 255, 255]))
            ],
            "Green": [ # AP Potions / Food / Elite items (greenish background)
                (np.array([36, 50, 50]), np.array([85, 255, 255]))
            ],
            "Blue": [ # Rare Books / Gems / Wood
                (np.array([86, 60, 60]), np.array([125, 255, 255]))
            ],
            "Purple": [ # Epic Books / Epic items
                (np.array([126, 60, 60]), np.array([165, 255, 255]))
            ]
        }

        max_pixels = 0
        dominant = "Unknown"
        total_pixels = crop_img.shape[0] * crop_img.shape[1]

        # 3. Pixel count per mask
        for color_name, ranges in colors.items():
            mask_count = 0
            for (lower, upper) in ranges:
                mask = cv2.inRange(hsv, lower, upper)
                mask_count += cv2.countNonZero(mask)
            
            # If the color occupies the most significant part so far
            if mask_count > max_pixels:
                max_pixels = mask_count
                dominant = color_name

        # Noise filter: If dominant color is very small (e.g. just white text), return Unknown
        if max_pixels < (total_pixels * 0.05): # Less than 5% of the area
            return "Unknown"

        return dominant