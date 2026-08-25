"use client";

import { useMemo } from "react";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { getInitials } from "@alumni/ui";
import type { AlumniMapMember } from "@/lib/member-api";
import { centroidForLocation } from "@/lib/country-centroids";

// Leaflet's default marker icon references image paths that break under
// bundlers (webpack/Turbopack rewrite the asset URLs); pointing at the
// package's own CDN copies is the standard workaround.
const markerIcon = L.icon({
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  iconRetinaUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});

// Deterministic small offset (not true random) so re-renders don't jitter
// markers around — just enough spread that alumni in the same country don't
// all sit on exactly one point.
function jitter(seed: string): [number, number] {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) | 0;
  const a = ((hash % 1000) / 1000) * 2 - 1;
  const b = (((hash >> 8) % 1000) / 1000) * 2 - 1;
  return [a * 1.4, b * 1.4];
}

export function AlumniMapView({ members }: { members: AlumniMapMember[] }) {
  const pins = useMemo(() => {
    return members
      .map((m) => {
        const centroid = centroidForLocation(m.location);
        if (!centroid) return null;
        const [dLat, dLng] = jitter(m.id);
        return { member: m, position: [centroid[0] + dLat, centroid[1] + dLng] as [number, number] };
      })
      .filter((p): p is { member: AlumniMapMember; position: [number, number] } => p !== null);
  }, [members]);

  return (
    <MapContainer
      center={[10, 10]}
      zoom={2}
      minZoom={2}
      style={{ height: "100%", width: "100%" }}
      scrollWheelZoom
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      {pins.map(({ member, position }) => (
        <Marker key={member.id} position={position} icon={markerIcon}>
          <Popup>
            <div className="flex items-center gap-2.5 min-w-[160px]">
              {member.profilePictureUrl ? (
                <img src={member.profilePictureUrl} alt="" className="w-8 h-8 rounded-full object-cover shrink-0" />
              ) : (
                <div className="w-8 h-8 rounded-full bg-primary/15 text-primary flex items-center justify-center text-[11px] font-bold shrink-0">
                  {getInitials(`${member.firstName} ${member.lastName}`)}
                </div>
              )}
              <div className="min-w-0">
                <p className="text-[13px] font-semibold leading-tight truncate">{member.firstName} {member.lastName}</p>
                <p className="text-[11.5px] text-muted-foreground truncate">{member.location} &middot; Class of {member.graduationYear}</p>
              </div>
            </div>
          </Popup>
        </Marker>
      ))}
    </MapContainer>
  );
}
