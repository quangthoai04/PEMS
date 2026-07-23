/**
 * Utility function to crop a face bounding box (0..1 normalized fractions)
 * into a clean 1:1 ratio square PNG Data URL for partner contact avatars.
 */
export async function cropFaceToDataUrl(
  imageUrl: string,
  box: { boundingBoxX: number; boundingBoxY: number; boundingBoxWidth: number; boundingBoxHeight: number },
  paddingFraction = 0.15,
  targetWidth = 256,
  targetHeight = 256
): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      try {
        const naturalW = img.naturalWidth;
        const naturalH = img.naturalHeight;

        // Calculate exact face bounding box in natural image pixels
        let faceX = box.boundingBoxX * naturalW;
        let faceY = box.boundingBoxY * naturalH;
        let faceW = box.boundingBoxWidth * naturalW;
        let faceH = box.boundingBoxHeight * naturalH;

        // Add proportional padding around the face for a nice portrait avatar
        const padX = faceW * paddingFraction;
        const padY = faceH * paddingFraction;

        faceX = Math.max(0, faceX - padX);
        faceY = Math.max(0, faceY - padY);
        faceW = Math.min(naturalW - faceX, faceW + padX * 2);
        faceH = Math.min(naturalH - faceY, faceH + padY * 2);

        // Make square aspect ratio
        const size = Math.max(faceW, faceH);

        // Render cropped face to canvas
        const canvas = document.createElement('canvas');
        canvas.width = targetWidth;
        canvas.height = targetHeight;
        const ctx = canvas.getContext('2d');
        if (!ctx) {
          reject(new Error('Canvas context unavailable'));
          return;
        }

        // Draw soft circular/rounded clipping or direct square draw
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, targetWidth, targetHeight);
        ctx.drawImage(img, faceX, faceY, size, size, 0, 0, targetWidth, targetHeight);

        const dataUrl = canvas.toDataURL('image/png');
        resolve(dataUrl);
      } catch (err) {
        reject(err);
      }
    };
    img.onerror = () => reject(new Error('Failed to load image for face cropping'));
    img.src = imageUrl;
  });
}
