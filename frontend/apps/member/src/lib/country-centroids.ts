// Approximate lat/lng centroids for plotting the Alumni Map — members only
// give a free-text "City, Country" location, not coordinates, so pins are
// placed at the country level (a small random jitter is applied per member
// so multiple alumni in the same country don't sit on exactly one point).
export const COUNTRY_CENTROIDS: Record<string, [number, number]> = {
  "ghana": [7.9465, -1.0232],
  "nigeria": [9.0820, 8.6753],
  "south africa": [-30.5595, 22.9375],
  "kenya": [-0.0236, 37.9062],
  "egypt": [26.8206, 30.8025],
  "morocco": [31.7917, -7.0926],
  "ethiopia": [9.1450, 40.4897],
  "tanzania": [-6.3690, 34.8888],
  "uganda": [1.3733, 32.2903],
  "senegal": [14.4974, -14.4524],
  "ivory coast": [7.5400, -5.5471],
  "cote d'ivoire": [7.5400, -5.5471],
  "cameroon": [7.3697, 12.3547],
  "zambia": [-13.1339, 27.8493],
  "zimbabwe": [-19.0154, 29.1549],
  "rwanda": [-1.9403, 29.8739],
  "togo": [8.6195, 0.8248],
  "benin": [9.3077, 2.3158],
  "burkina faso": [12.2383, -1.5616],
  "mali": [17.5707, -3.9962],
  "sierra leone": [8.4606, -11.7799],
  "liberia": [6.4281, -9.4295],
  "gambia": [13.4432, -15.3101],
  "gabon": [-0.8037, 11.6094],
  "namibia": [-22.9576, 18.4904],
  "botswana": [-22.3285, 24.6849],
  "mozambique": [-18.6657, 35.5296],
  "malawi": [-13.2543, 34.3015],
  "sudan": [12.8628, 30.2176],
  "algeria": [28.0339, 1.6596],
  "tunisia": [33.8869, 9.5375],
  "libya": [26.3351, 17.2283],
  "united states": [37.0902, -95.7129],
  "usa": [37.0902, -95.7129],
  "united states of america": [37.0902, -95.7129],
  "canada": [56.1304, -106.3468],
  "united kingdom": [55.3781, -3.4360],
  "uk": [55.3781, -3.4360],
  "england": [52.3555, -1.1743],
  "scotland": [56.4907, -4.2026],
  "germany": [51.1657, 10.4515],
  "france": [46.2276, 2.2137],
  "netherlands": [52.1326, 5.2913],
  "belgium": [50.5039, 4.4699],
  "switzerland": [46.8182, 8.2275],
  "sweden": [60.1282, 18.6435],
  "norway": [60.4720, 8.4689],
  "denmark": [56.2639, 9.5018],
  "finland": [61.9241, 25.7482],
  "ireland": [53.4129, -8.2439],
  "spain": [40.4637, -3.7492],
  "portugal": [39.3999, -8.2245],
  "italy": [41.8719, 12.5674],
  "poland": [51.9194, 19.1451],
  "austria": [47.5162, 14.5501],
  "greece": [39.0742, 21.8243],
  "turkey": [38.9637, 35.2433],
  "russia": [61.5240, 105.3188],
  "ukraine": [48.3794, 31.1656],
  "china": [35.8617, 104.1954],
  "japan": [36.2048, 138.2529],
  "south korea": [35.9078, 127.7669],
  "india": [20.5937, 78.9629],
  "pakistan": [30.3753, 69.3451],
  "bangladesh": [23.6850, 90.3563],
  "indonesia": [-0.7893, 113.9213],
  "malaysia": [4.2105, 101.9758],
  "singapore": [1.3521, 103.8198],
  "philippines": [12.8797, 121.7740],
  "thailand": [15.8700, 100.9925],
  "vietnam": [14.0583, 108.2772],
  "saudi arabia": [23.8859, 45.0792],
  "united arab emirates": [23.4241, 53.8478],
  "uae": [23.4241, 53.8478],
  "qatar": [25.3548, 51.1839],
  "kuwait": [29.3117, 47.4818],
  "israel": [31.0461, 34.8516],
  "lebanon": [33.8547, 35.8623],
  "jordan": [30.5852, 36.2384],
  "australia": [-25.2744, 133.7751],
  "new zealand": [-40.9006, 174.8860],
  "brazil": [-14.2350, -51.9253],
  "mexico": [23.6345, -102.5528],
  "argentina": [-38.4161, -63.6167],
  "chile": [-35.6751, -71.5430],
  "colombia": [4.5709, -74.2973],
  "peru": [-9.1900, -75.0152],
  "jamaica": [18.1096, -77.2975],
  "trinidad and tobago": [10.6918, -61.2225],
};

export function centroidForLocation(location: string | undefined | null): [number, number] | null {
  if (!location) return null;
  const parts = location.split(",").map((p) => p.trim().toLowerCase()).filter(Boolean);
  if (parts.length === 0) return null;
  // "City, Country" — country is the last segment; fall back to matching
  // the whole string in case someone entered just a country name.
  const country = parts[parts.length - 1];
  return COUNTRY_CENTROIDS[country] ?? COUNTRY_CENTROIDS[parts.join(", ")] ?? null;
}

export function countryLabel(location: string | undefined | null): string | null {
  if (!location) return null;
  const parts = location.split(",").map((p) => p.trim()).filter(Boolean);
  return parts.length ? parts[parts.length - 1] : null;
}
