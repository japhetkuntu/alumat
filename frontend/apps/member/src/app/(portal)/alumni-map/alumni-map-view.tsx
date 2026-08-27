"use client";

import { useEffect, useMemo, useRef } from "react";
import { MapContainer, TileLayer, ZoomControl, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import "leaflet.markercluster";
import "leaflet.markercluster/dist/MarkerCluster.css";
import "leaflet.markercluster/dist/MarkerCluster.Default.css";
import { getInitials } from "@alumni/ui";
import type { AlumniMapMember } from "@/lib/member-api";
import { centroidForLocation } from "@/lib/country-centroids";

// Reads the app's actual brand color at render time (set as a CSS var on
// :root) so pins/clusters match whatever institution this portal belongs
// to, rather than a hardcoded color.
function primaryColor(): string {
  if (typeof window === "undefined") return "#2563eb";
  const v = getComputedStyle(document.documentElement).getPropertyValue("--primary").trim();
  return v || "#2563eb";
}

// A rounded teardrop pin (not Leaflet's default blue marker) with the
// member's initials inside — reads instantly at world-zoom without needing
// to open every popup, and matches the app's own visual language instead of
// looking like an unstyled third-party map.
function pinIcon(member: AlumniMapMember): L.DivIcon {
  const initials = getInitials(`${member.firstName} ${member.lastName}`);
  const color = primaryColor();
  const html = `
    <div class="alumni-pin" style="--pin-color:${color}">
      <span class="alumni-pin__avatar">${
        member.profilePictureUrl
          ? `<img src="${member.profilePictureUrl}" alt="" />`
          : `<span>${initials}</span>`
      }</span>
    </div>
  `;
  return L.divIcon({
    html,
    className: "alumni-pin-wrapper",
    iconSize: [34, 42],
    iconAnchor: [17, 40],
    popupAnchor: [0, -38],
  });
}

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

type Pin = { member: AlumniMapMember; position: [number, number] };

/** Groups nearby pins into a count badge at low zoom, matching the app's brand color, so alumni clustered in the same city/country don't just overlap into an unreadable pile. */
function ClusterLayer({ pins }: { pins: Pin[] }) {
  const map = useMap();
  const groupRef = useRef<L.MarkerClusterGroup | null>(null);

  useEffect(() => {
    const color = primaryColor();
    const group = L.markerClusterGroup({
      showCoverageOnHover: false,
      spiderfyOnMaxZoom: true,
      maxClusterRadius: 42,
      iconCreateFunction: (cluster) => {
        const count = cluster.getChildCount();
        const size = count >= 50 ? 52 : count >= 10 ? 44 : 36;
        return L.divIcon({
          html: `<div class="alumni-cluster" style="--pin-color:${color}; width:${size}px; height:${size}px;"><span>${count}</span></div>`,
          className: "alumni-cluster-wrapper",
          iconSize: [size, size],
        });
      },
    });
    groupRef.current = group;
    map.addLayer(group);
    return () => {
      map.removeLayer(group);
      groupRef.current = null;
    };
  }, [map]);

  useEffect(() => {
    const group = groupRef.current;
    if (!group) return;
    group.clearLayers();
    for (const { member, position } of pins) {
      const marker = L.marker(position, { icon: pinIcon(member) });
      const initials = getInitials(`${member.firstName} ${member.lastName}`);
      const avatar = member.profilePictureUrl
        ? `<img src="${member.profilePictureUrl}" alt="" class="alumni-popup__avatar-img" />`
        : `<span class="alumni-popup__avatar-fallback">${initials}</span>`;
      marker.bindPopup(
        `<div class="alumni-popup">
          <span class="alumni-popup__avatar">${avatar}</span>
          <div class="alumni-popup__body">
            <p class="alumni-popup__name">${member.firstName} ${member.lastName}</p>
            <p class="alumni-popup__meta">${member.location ?? ""}${member.location ? " &middot; " : ""}Class of ${member.graduationYear}</p>
          </div>
        </div>`,
        { className: "alumni-popup-wrapper", closeButton: false }
      );
      group.addLayer(marker);
    }
  }, [pins]);

  return null;
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
      .filter((p): p is Pin => p !== null);
  }, [members]);

  return (
    <MapContainer
      center={[10, 10]}
      zoom={2}
      minZoom={2}
      maxZoom={9}
      style={{ height: "100%", width: "100%" }}
      scrollWheelZoom
      zoomControl={false}
      worldCopyJump
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        maxZoom={19}
      />
      <ZoomControl position="bottomright" />
      <ClusterLayer pins={pins} />
    </MapContainer>
  );
}
