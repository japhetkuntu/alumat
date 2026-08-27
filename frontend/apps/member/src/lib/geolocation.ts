/**
 * Requests the browser's real geolocation, rounded to 1 decimal degree
 * (~11km) before it ever leaves the device — accurate enough to place a pin
 * in roughly the right city/region for the Alumni Map, never precise enough
 * to reveal someone's exact address. The backend rounds again on save as a
 * second line of defense, but the point is to never transmit the raw
 * high-precision coordinates in the first place.
 */
export function getRoundedLocation(): Promise<{ latitude: number; longitude: number }> {
  return new Promise((resolve, reject) => {
    if (typeof navigator === "undefined" || !("geolocation" in navigator)) {
      reject(new Error("Your browser doesn't support location access."));
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (position) => {
        resolve({
          latitude: Math.round(position.coords.latitude * 10) / 10,
          longitude: Math.round(position.coords.longitude * 10) / 10,
        });
      },
      (error) => {
        reject(new Error(
          error.code === error.PERMISSION_DENIED
            ? "Location access was denied. Enable it in your browser's site settings to appear on the map."
            : "Couldn't determine your location. Please try again."
        ));
      },
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 5 * 60_000 }
    );
  });
}
