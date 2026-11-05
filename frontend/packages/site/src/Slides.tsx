"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export interface Slide { id: string; img?: string; title?: string; text?: string; href?: string; external?: boolean }

const EVERY = 6500;

/**
 * The rotating banner. It turns by itself, stops while the visitor points at it, focuses inside it or has asked their device for less
 * motion, and always has buttons, dots and arrow keys. A single slide is just a banner.
 */
export function Slides({ slides, label }: { slides: Slide[]; label: string }) {
  const [i, setI] = useState(0);
  const [paused, setPaused] = useState(false);
  const [still, setStill] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  const n = slides.length;
  const go = useCallback((to: number) => setI(((to % n) + n) % n), [n]);

  useEffect(() => { setStill(window.matchMedia("(prefers-reduced-motion: reduce)").matches); }, []);
  useEffect(() => {
    if (n < 2 || paused || still) return;
    const t = window.setTimeout(() => go(i + 1), EVERY);
    return () => window.clearTimeout(t);
  }, [i, n, paused, still, go]);

  return (
    <div className="slides" ref={box} role="region" aria-roledescription="carousel" aria-label={label}
      onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)} onBlur={() => setPaused(false)}
      onKeyDown={(e) => { if (e.key === "ArrowRight") go(i + 1); else if (e.key === "ArrowLeft") go(i - 1); }}>
      {slides.map((s, k) => (
        <div key={s.id} className={`slide${k === i ? " on" : ""}`} role="group" aria-roledescription="slide" aria-label={`${k + 1} of ${n}`} aria-hidden={k !== i}>
          {s.img && /* eslint-disable-next-line @next/next/no-img-element */ <img src={s.img} alt="" loading={k === 0 ? "eager" : "lazy"} decoding="async" />}
          {(s.title || s.text || s.href) && (
            <div className="scap">
              {s.title && <h2>{s.title}</h2>}
              {s.text && <p>{s.text}</p>}
              {s.href && <a className="btn" href={s.href} tabIndex={k === i ? 0 : -1} {...(s.external ? { target: "_blank", rel: "noopener noreferrer" } : {})}>Find out more</a>}
            </div>
          )}
        </div>
      ))}
      {n > 1 && (
        <>
          <button type="button" className="snav prev" aria-label="Previous slide" onClick={() => go(i - 1)}>‹</button>
          <button type="button" className="snav next" aria-label="Next slide" onClick={() => go(i + 1)}>›</button>
          <div className="sdots">{slides.map((s, k) => <button key={s.id} type="button" aria-label={`Show slide ${k + 1}`} aria-current={k === i} className={k === i ? "on" : ""} onClick={() => go(k)} />)}</div>
        </>
      )}
    </div>
  );
}
