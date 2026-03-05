import cv2
import numpy as np
import base64
import logging

# Initialize module logger
logger = logging.getLogger(__name__)

class ImageProcessor:
    
    @staticmethod
    def base64_to_cv2(b64_str: str):
        """Converts Base64 string to OpenCV format (BGR)."""
        try:
            # Strip data URI scheme header if present (e.g., "data:image/png;base64,")
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
            # INTER_AREA interpolation is ideal for downscaling to preserve text sharpness
            resized = cv2.resize(img, (max_width, new_h), interpolation=cv2.INTER_AREA)
            return resized, ratio
        return img, 1.0

    @staticmethod
    def isolate_paper(img):
        """
        Attempts to isolate the beige paper from the background using HSV thresholding 
        and Perspective Transform. Falls back to a central crop if detection fails.
        Returns: (processed_image, is_isolated_boolean)
        """
        if img is None:
            return None, False

        h_orig, w_orig = img.shape[:2]
        hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
        
        # Domain-specific beige color range calibrated for RoK UI
        lower_beige = np.array([5, 15, 90])
        upper_beige = np.array([40, 180, 255])
        
        mask = cv2.inRange(hsv, lower_beige, upper_beige)
        
        # Morphological closing to fill gaps and reduce noise
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (25, 25))
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        
        if contours:
            cnt = max(contours, key=cv2.contourArea)
            
            # Minimum area threshold: Contour must cover at least 15% of total image area
            if cv2.contourArea(cnt) > (h_orig * w_orig * 0.15):
                rect_min = cv2.minAreaRect(cnt)
                box_pts = np.array(cv2.boxPoints(rect_min), dtype="float32")
                
                # Enforce point ordering: top-left, top-right, bottom-right, bottom-left
                rect = np.zeros((4, 2), dtype="float32")
                s = box_pts.sum(axis=1)
                rect[0] = box_pts[np.argmin(s)]
                rect[2] = box_pts[np.argmax(s)]
                diff = np.diff(box_pts, axis=1)
                rect[1] = box_pts[np.argmin(diff)]
                rect[3] = box_pts[np.argmax(diff)]

                (tl, tr, br, bl) = rect
                
                # Compute dimensions for the unwarped perspective
                widthA = np.sqrt(((br[0] - bl[0]) ** 2) + ((br[1] - bl[1]) ** 2))
                widthB = np.sqrt(((tr[0] - tl[0]) ** 2) + ((tr[1] - tl[1]) ** 2))
                heightA = np.sqrt(((tr[0] - br[0]) ** 2) + ((tr[1] - br[1]) ** 2))
                heightB = np.sqrt(((tl[0] - bl[0]) ** 2) + ((tl[1] - bl[1]) ** 2))
                
                maxWidth = int(max(widthA, widthB))
                maxHeight = int(max(heightA, heightB))
                
                dst = np.array([[0, 0], 
                    [maxWidth-1, 0],[maxWidth-1, maxHeight-1], 
                    [0, maxHeight-1]
                ], dtype="float32")
                
                M = cv2.getPerspectiveTransform(rect, dst)
                warped = cv2.warpPerspective(img, M, (maxWidth, maxHeight))
                
                return warped, True

        # FALLBACK: Safe Central Crop
        # If paper isolation fails, assume the target report is roughly centered
        y1, y2 = int(h_orig * 0.12), int(h_orig * 0.88)
        x1, x2 = int(w_orig * 0.15), int(w_orig * 0.85)
        
        y1, y2 = max(0, y1), min(h_orig, y2)
        x1, x2 = max(0, x1), min(w_orig, x2)
        
        return img[y1:y2, x1:x2], False

    @staticmethod
    def apply_filters(img):
        """Applies a sharpening convolution kernel to enhance text edges."""
        if img is None: return None
        sharpen_kernel = np.array([[-1,-1,-1], [-1,9,-1],[-1,-1,-1]])
        return cv2.filter2D(img, -1, sharpen_kernel)

    @staticmethod
    def process_region(full_img, x, y, w, h, strategy="default"):
        """
        Crops a specific coordinate region and applies heuristic image filters 
        based on the provided strategy. Used extensively for Batch/Magnifier processing.
        """
        if full_img is None: return None
        ih, iw = full_img.shape[:2]
        
        # Enforce safety bounds to prevent out-of-index exceptions
        x, y = max(0, x), max(0, y)
        w, h = min(w, iw - x), min(h, ih - y)
        
        if w <= 0 or h <= 0:
            return None
        
        crop = full_img[y:y+h, x:x+w]

        # Upscale factor to improve Tesseract OCR accuracy on small/blurry numerics
        # INTER_CUBIC is optimal for localized enlargements
        crop = cv2.resize(crop, None, fx=3.0, fy=3.0, interpolation=cv2.INTER_CUBIC)

        if strategy == "HighContrastBinary":
            gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
            _, binary = cv2.threshold(gray, 150, 255, cv2.THRESH_BINARY)
            return binary

        elif strategy == "InvertedBinary":
            gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
            # Invert polarity to match OCR baseline (requires black text on white background)
            gray = cv2.bitwise_not(gray)
            _, binary = cv2.threshold(gray, 120, 255, cv2.THRESH_BINARY)
            return binary

        # --- STRATEGY: WHITE ISOLATION ---
        elif strategy == "WhiteIsolation":
            # Convert to HLS (Hue, Lightness, Saturation). 
            # The Lightness channel handles pure white detection irrespective of background noise.
            hls = cv2.cvtColor(crop, cv2.COLOR_BGR2HLS)
            
            # Defines "White" threshold (L > 180 captures bright whites and shiny numeric text)
            lower_white = np.array([0, 180, 0])
            upper_white = np.array([255, 255, 255])
            
            mask = cv2.inRange(hls, lower_white, upper_white)
            
            # Morphological opening removes isolated background noise pixels
            kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (2, 2))
            mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
            
            # Mild dilation thickens thin font strokes (e.g., "1"), preventing OCR stroke loss
            mask = cv2.dilate(mask, kernel, iterations=1)
            
            final = cv2.bitwise_not(mask)
            return final

        elif strategy == "Sharpen":
            kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
            return cv2.filter2D(crop, -1, kernel)
        
        elif strategy == "ShieldAnalysis":
            hsv = cv2.cvtColor(crop, cv2.COLOR_BGR2HSV)
            h, w = crop.shape[:2]

            # Targets cyan/light blue. Ranges calibrated to capture bright borders and transparent centers
            lower_cyan = np.array([80, 20, 70])   
            upper_cyan = np.array([130, 255, 255]) 
            mask = cv2.inRange(hsv, lower_cyan, upper_cyan)

            # Morphological opening removes background noise (grass/rivers), 
            # while closing reconnects fragmented shield edges.
            kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
            mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
            mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=2)

            contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            
            if not contours:
                return "FALSE"

            # Extract largest contour; the target shield should be the dominant cyan object
            largest_contour = max(contours, key=cv2.contourArea)
            area = cv2.contourArea(largest_contour)
            
            # Minimum Size Filter: Target area must be > 2% of total search zone
            min_area_threshold = (h * w) * 0.02
            
            if area < min_area_threshold:
                return "FALSE"

            # GEOMETRIC VALIDATION
            # Evaluates horizontal alignment of the shield relative to the crop center
            x, y, cw, ch = cv2.boundingRect(largest_contour)
            shield_center_x = x + (cw / 2)
            image_center_x = w / 2
            
            # Maximum allowed horizontal deviation from center: 25% of width
            deviation = abs(shield_center_x - image_center_x)
            max_deviation = w * 0.25 

            if deviation > max_deviation:
                # Discards off-center objects (likely adjacent nodes/shields)
                return "FALSE"

            # Aspect Ratio Validation: Filters out long entities (e.g., rivers)
            aspect_ratio = float(cw) / ch
            if aspect_ratio > 3.0 or aspect_ratio < 0.3:
                return "FALSE" 

            return "TRUE"
            
        # --- STRATEGY: MAP LABEL ---
        elif strategy == "MapLabel":
            gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
            inverted = cv2.bitwise_not(gray)
            
            # CLAHE (Contrast Limited Adaptive Histogram Equalization) minimizes shadow gradients
            clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8,8))
            contrast_enhanced = clahe.apply(inverted)
            
            kernel = np.array([[-1,-1,-1], [-1,9,-1],[-1,-1,-1]])
            final = cv2.filter2D(contrast_enhanced, -1, kernel)
            
            return final
            
        # --- STRATEGY: TROOP ICON COLOR DETECTION ---
        elif strategy == "TroopColor":
            # Normalizes icon resolution to stabilize color evaluation statistics
            icon = cv2.resize(crop, (50, 50), interpolation=cv2.INTER_AREA)
            
            # Delegates to specialized troop tier color detection method
            color = ImageProcessor.detect_troop_tier_color(icon)
            
            # WORKAROUND: Generate a dummy canvas with the color text for the OCR engine to parse seamlessly
            dummy_canvas = np.ones((50, 200), dtype=np.uint8) * 255 
            cv2.putText(dummy_canvas, color, (10, 35), 
                        cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 0, 0), 2)
            
            return dummy_canvas

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

        hsv = cv2.cvtColor(crop_img, cv2.COLOR_BGR2HSV)
        
        # Domain-specific color ranges calibrated to RoK UI art style
        colors = {
            "Purple":[ (np.array([120, 40, 40]), np.array([165, 255, 255])) ], # T4
            
            # UPDATED: Minimum saturation and brightness increased to 160 and 130.
            # This effectively masks out the dark UI background, ensuring only 
            # the vibrant "sky" blue of T3 shields is detected.
            "Blue":   [ (np.array([95, 160, 130]), np.array([125, 255, 255])) ],  
            
            "Green":  [ (np.array([40, 50, 50]), np.array([85, 255, 255])) ],     # T2
            "Red":[ (np.array([0, 70, 50]), np.array([10, 255, 255])), 
                        (np.array([170, 70, 50]), np.array([180, 255, 255])) ],   # T1
            "Gold":   [ (np.array([15, 120, 120]), np.array([35, 255, 255])) ]    # T5
        }

        max_pixels = 0
        dominant = "Unknown"
        total_pixels = crop_img.shape[0] * crop_img.shape[1]

        # Aggregate non-zero pixels for each target hue range
        for color_name, ranges in colors.items():
            mask_count = 0
            for (lower, upper) in ranges:
                mask = cv2.inRange(hsv, lower, upper)
                mask_count += cv2.countNonZero(mask)
            
            if mask_count > max_pixels:
                max_pixels = mask_count
                dominant = color_name

        # Noise filter: Rejects dominant colors occupying less than 5% of total area
        if max_pixels < (total_pixels * 0.05):
            return "Unknown"

        return dominant
    
    @staticmethod
    def detect_troop_tier_color(crop_img):
        """
        Dedicated method for detecting Troop Tier colors from shield icons.
        Optimized to analyze only the top half of the crop using priority-based logic.
        """
        if crop_img is None or crop_img.size == 0:
            return "Unknown"

        h, w = crop_img.shape[:2]
        
        # TOP-HALF STRATEGY: Crop the upper section to isolate the shield's "sky" background,
        # ignoring the bottom half which contains the blue Roman numeral ribbons (e.g., IV, V).
        top_half = crop_img[0:int(h * 0.55), 0:w]

        hsv = cv2.cvtColor(top_half, cv2.COLOR_BGR2HSV)
        
        # Calibrated HSV ranges.
        # Note: The Gold mask will capture shield borders, making it highly prevalent in both T4 and T5 icons.
        colors = {
            "Purple":[ (np.array([120, 40, 40]), np.array([165, 255, 255])) ], # T4
            "Blue":[ (np.array([95, 100, 100]), np.array([125, 255, 255])) ],  # T3
            "Green":[ (np.array([40, 50, 50]), np.array([85, 255, 255])) ],     # T2
            "Red":    [ (np.array([0, 70, 50]), np.array([10, 255, 255])), 
                        (np.array([170, 70, 50]), np.array([180, 255, 255])) ],   # T1
            "Gold":   [ (np.array([15, 120, 120]), np.array([35, 255, 255])) ]    # T5
        }

        color_counts = {}
        total_pixels = top_half.shape[0] * top_half.shape[1]

        for color_name, ranges in colors.items():
            mask_count = 0
            for (lower, upper) in ranges:
                mask = cv2.inRange(hsv, lower, upper)
                mask_count += cv2.countNonZero(mask)
            color_counts[color_name] = mask_count

        # HIERARCHY CHECK: Since T4 shields have gold borders, "Gold" pixel counts 
        # will be high across multiple tiers. We evaluate top-down; the presence 
        # of specific background colors dictates the actual tier.
        
        # Minimum area threshold: 8% of the top-half crop
        threshold = total_pixels * 0.08

        if color_counts["Purple"] > threshold: return "Purple"
        if color_counts["Blue"] > threshold: return "Blue"
        if color_counts["Green"] > threshold: return "Green"
        if color_counts["Red"] > threshold: return "Red"
        
        # Fallback: If no lower tier colors match but a significant amount 
        # of Gold is present, it is classified as T5.
        if color_counts["Gold"] > threshold: return "Gold"

        return "Unknown"