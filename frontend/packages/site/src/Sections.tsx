import { BLOCKS, portalPath } from "./blocks";
import type { SiteCtx } from "./context";
import { dateOf, dayOf, group, money, monthOf, pct } from "./format";
import { Photo } from "./Photo";
import { Ed, Rich } from "./Text";
import { initials } from "./color";
import { isExternal, linkHref, linkOk } from "./links";
import type { SectionType, SiteItem, SiteSection } from "./types";
import { SECTION_ICONS } from "./icons";
import { hasContent } from "./visibility";

const ARW = <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6" /></svg>;
const BAG = <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 8h14l-1 12H6zM9 8V6a3 3 0 016 0v2" /></svg>;
const icon = (i: number) => <span className="ico" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" dangerouslySetInnerHTML={{ __html: SECTION_ICONS[i % SECTION_ICONS.length] }} /></span>;

interface P { sec: SiteSection; ctx: SiteCtx }

/** A button the admin writes: its words are edited in place, and where it goes is set in the side panel. */
function Btn({ sec, ctx, k, cls = "", item, label }: P & { k: string; cls?: string; item?: SiteItem; label: string }) {
  const l = (item ?? sec).links[k];
  const ok = linkOk(l);
  const edit = ctx.edit;
  const text = <Ed value={label} edit={edit} sectionId={sec.id} itemId={item?.id} field={k} />;
  if (edit) return <a className={`btn ${cls} ${ok ? "" : "nolink"}`} title={ok ? edit.describeLink(sec, k, item?.id) : "This button does not go anywhere yet"} onClick={(e) => e.preventDefault()}>{text}</a>;
  const href = linkHref(l, ctx.sections);
  if (!href || !label) return null;
  return <a className={`btn ${cls}`} href={href} {...(isExternal(l) ? { target: "_blank", rel: "noopener noreferrer" } : {})}>{label}</a>;
}

/** A link to a portal page. Inside the editor it only points, so clicking it never leaves the editor. */
function Go({ ctx, href, className, children, title }: { ctx: SiteCtx; href: string; className?: string; children: React.ReactNode; title?: string }) {
  if (ctx.edit) return <a className={className} title={title ?? `Opens ${href} in your member portal`} onClick={(e) => e.preventDefault()}>{children}</a>;
  return <a className={className} href={href}>{children}</a>;
}

function Eyebrow({ sec, ctx }: P) {
  const v = sec.props.eyebrow;
  if (!ctx.edit && !v) return null;
  if (ctx.edit && v === undefined) return null;
  return <span className="eyebrow"><Ed value={v} edit={ctx.edit} sectionId={sec.id} field="eyebrow" placeholder="Label" /></span>;
}

function SecHead({ sec, ctx }: P) {
  const b = BLOCKS[sec.type];
  return (
    <div className="shead">
      <div className="hl">
        <Eyebrow sec={sec} ctx={ctx} />
        <h2 className="h-lg"><Ed value={sec.props.heading} edit={ctx.edit} sectionId={sec.id} field="heading" placeholder="Heading" /></h2>
      </div>
      {ctx.edit && b.live && <span className="livebadge" title="Filled in from your own items. In the editor these are sample items.">Live from your {b.live.toLowerCase()}</span>}
      {b.allHref && <Go ctx={ctx} href={b.allHref} className="viewall">View all{ARW}</Go>}
    </div>
  );
}

const Price = ({ v, small }: { v: string; small?: string }) => <span className="price"><small>{small ?? "Price"}</small>{v}</span>;

export function SectionBody({ sec, ctx }: P): React.ReactNode {
  const { data, edit } = ctx, v = sec.variant, n = Math.max(1, sec.count);
  const cur = ctx.currency;
  // In the editor, a live section with nothing to show says so plainly instead of showing made-up items.
  const live = BLOCKS[sec.type];
  if (edit && live.live && !hasContent(sec, data)) {
    const where = live.feature ?? live.name;
    return (
      <div className="wrap">
        {sec.type !== "stats" && <SecHead sec={sec} ctx={ctx} />}
        <div className="emptylive" role="note">
          <b>{sec.type === "stats" ? "Your community numbers" : `Fills itself from your ${live.live.toLowerCase()}`}</b>
          <span>{sec.type === "stats" ? "Visitors see your real member and event numbers here." : `Visitors see your own ${live.live.toLowerCase()} here${live.feature ? " once you mark them “Show on website”" : ""}. If there are none, visitors do not see this section at all.`}</span>
          <small>Use “Show example content” at the top to preview the layout with made-up items. They are never published.</small>
        </div>
      </div>
    );
  }
  switch (sec.type) {
    case "hero": return <Hero sec={sec} ctx={ctx} />;
    case "text":
      return (
        <div className={`wrap ${v === "one" ? "narrow" : ""}`}>
          <Eyebrow sec={sec} ctx={ctx} />
          <h2 className="h-lg" style={{ marginBottom: 20 }}><Ed value={sec.props.heading} edit={edit} sectionId={sec.id} field="heading" /></h2>
          <div className={v === "two" ? "cols2" : ""}>
            <Rich value={sec.props.body} edit={edit} sectionId={sec.id} field="body" />
            {(sec.props.body2 || edit) && <div style={{ marginTop: 12 }}><Rich value={sec.props.body2} edit={edit} sectionId={sec.id} field="body2" /></div>}
          </div>
        </div>
      );
    case "imagetext":
      return (
        <div className="wrap"><div className={`split ${v === "right" ? "flip" : ""}`}>
          <Photo src={sec.imgs.main} alt="" className="" width={900} edit={edit} sectionId={sec.id} slot="main" />
          <div>
            <Eyebrow sec={sec} ctx={ctx} />
            <h2 className="h-lg"><Ed value={sec.props.heading} edit={edit} sectionId={sec.id} field="heading" /></h2>
            <div style={{ marginTop: 16 }}><Rich value={sec.props.body} edit={edit} sectionId={sec.id} field="body" /></div>
            {(sec.props.link || edit) && <div className="btns"><Btn sec={sec} ctx={ctx} k="link" cls="ghost" label={sec.props.link ?? ""} /></div>}
          </div>
        </div></div>
      );
    case "news": {
      const list = (data.news ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          {v === "grid" ? (
            <div className="bal cards" style={{ ["--n" as string]: list.length }}>
              {list.map((x) => (
                <article className="card link" key={x.id}>
                  <Photo src={x.imageUrl} width={700} className="newsimg" />
                  <div className="b">
                    <time dateTime={x.publishedAt ?? undefined}>{dateOf(x.publishedAt)}</time>
                    <h3>{x.title}</h3>{x.excerpt && <p>{x.excerpt}</p>}
                    <Go ctx={ctx} href={portalPath.news(x.id)} className="more">Read more{ARW}</Go>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <div className="news-list">{list.map((x) => (
              <article className="link" key={x.id}><time dateTime={x.publishedAt ?? undefined}>{dateOf(x.publishedAt)}</time>
                <div><h3><Go ctx={ctx} href={portalPath.news(x.id)}>{x.title}</Go></h3>{x.excerpt && <p>{x.excerpt}</p>}</div></article>
            ))}</div>
          )}
        </div>
      );
    }
    case "events": {
      const list = (data.events ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          {v === "cards" ? (
            <div className="bal cards" style={{ ["--n" as string]: list.length }}>{list.map((x) => (
              <article className="card link" key={x.id}><div className="b">
                <div className="date" style={{ display: "inline-block", padding: "8px 14px", marginBottom: 12 }}><b>{dayOf(x.startDate)}</b><small>{monthOf(x.startDate)}</small></div>
                <h3>{x.title}</h3>{x.venue && <p>{x.venue}</p>}
                <Go ctx={ctx} href={portalPath.events(x.id)} className="more">Details{ARW}</Go>
              </div></article>
            ))}</div>
          ) : (
            <div className="ev-list">{list.map((x) => (
              <article className="link" key={x.id}>
                <div className="date"><b>{dayOf(x.startDate)}</b><small>{monthOf(x.startDate)}</small></div>
                <div><h3>{x.title}</h3>{x.venue && <p>{x.venue}</p>}</div>
                <Go ctx={ctx} href={portalPath.events(x.id)} className="btn sm ghost">Details{ARW}</Go>
              </article>
            ))}</div>
          )}
        </div>
      );
    }
    case "stats": {
      const s = data.stats ?? {};
      const rows: [number | null | undefined, string][] = [[s.members, "members"], [s.joinedLast30Days, "joined this month"], [s.eventsNext30Days, "events coming up"], [s.openJobs, "open jobs"], [s.businesses, "member businesses"], [s.communities, "communities"]];
      const shown = rows.filter(([x]) => (x ?? 0) > 0).slice(0, 4);
      return <div className="wrap"><div className="stats" style={{ ["--n" as string]: shown.length }}>{shown.map(([x, l]) => <div key={l}><b data-count={x!}>{group(x!)}</b><span>{l}</span></div>)}</div></div>;
    }
    case "gallery": {
      const its = sec.items ?? [];
      const mos = v === "mosaic" && its.length >= 5 && its.length % 2 === 1;
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          {its.length ? (
            <div className={`gal ${mos ? "mosaic" : "bal"}`} style={{ ["--n" as string]: its.length, ["--span" as string]: Math.ceil((its.length - 1) / 2) }}>
              {its.map((it) => (
                <div className="cell" key={it.id}>
                  <Photo src={sec.imgs["i" + it.id]} alt={it.fields.alt ?? ""} width={700} edit={edit} sectionId={sec.id} slot={"i" + it.id} />
                  {edit && <button type="button" className="itx" onClick={(e) => { e.stopPropagation(); edit.removeItem(sec.id, it.id); }}>Remove</button>}
                </div>
              ))}
            </div>
          ) : edit ? <button type="button" className="emptybox" onClick={(e) => { e.stopPropagation(); edit.addItem(sec.id); }}>Add your first photo</button> : null}
          <AddItem sec={sec} ctx={ctx} />
        </div>
      );
    }
    case "leaders": {
      const its = sec.items ?? [];
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          {its.length ? (
            <div className="bal ppl" style={{ ["--n" as string]: its.length }}>
              {its.map((it) => (
                <div className="pcell" key={it.id}>
                  {sec.imgs["i" + it.id] || edit ? <Photo src={sec.imgs["i" + it.id]} alt={it.fields.name} width={500} edit={edit} sectionId={sec.id} slot={"i" + it.id} prompt="Add photo" className={sec.imgs["i" + it.id] ? "" : "mono"} /> : <div className="img mono" data-mono={initials(it.fields.name)} />}
                  <h3><Ed value={it.fields.name} edit={edit} sectionId={sec.id} itemId={it.id} field="name" placeholder="Name" /></h3>
                  <p><Ed value={it.fields.role} edit={edit} sectionId={sec.id} itemId={it.id} field="role" placeholder="Role" /></p>
                  {edit && <button type="button" className="itx" onClick={(e) => { e.stopPropagation(); edit.removeItem(sec.id, it.id); }}>Remove</button>}
                </div>
              ))}
            </div>
          ) : edit ? <button type="button" className="emptybox" onClick={(e) => { e.stopPropagation(); edit.addItem(sec.id); }}>Add the first person</button> : null}
          <AddItem sec={sec} ctx={ctx} />
        </div>
      );
    }
    case "faq":
      return (
        <div className="wrap narrow faq">
          <h2 className="h-lg" style={{ marginBottom: 24 }}><Ed value={sec.props.heading} edit={edit} sectionId={sec.id} field="heading" /></h2>
          {(sec.items ?? []).map((it) => (
            <details key={it.id} open={!!edit}>
              <summary><Ed value={it.fields.q} edit={edit} sectionId={sec.id} itemId={it.id} field="q" placeholder="Question" /></summary>
              <div className="ans"><Rich value={it.fields.a} edit={edit} sectionId={sec.id} itemId={it.id} field="a" /></div>
              {edit && <button type="button" className="itx" onClick={(e) => { e.stopPropagation(); edit.removeItem(sec.id, it.id); }}>Remove this question</button>}
            </details>
          ))}
          {edit && !(sec.items ?? []).length && <button type="button" className="emptybox" onClick={(e) => { e.stopPropagation(); edit.addItem(sec.id); }}>Add the first question</button>}
          <AddItem sec={sec} ctx={ctx} />
        </div>
      );
    case "cta":
      return (
        <div className="wrap narrow cta">
          <h2 className="h-lg"><Ed value={sec.props.heading} edit={edit} sectionId={sec.id} field="heading" /></h2>
          <div className="lead" style={{ marginLeft: "auto", marginRight: "auto" }}><Rich value={sec.props.body} edit={edit} sectionId={sec.id} field="body" /></div>
          <div className="btns"><Btn sec={sec} ctx={ctx} k="b1" label={sec.props.b1 ?? ""} /></div>
        </div>
      );
    case "cards": return <Cards sec={sec} ctx={ctx} />;
    case "contact":
      return (
        <div className="wrap"><div className="contact">
          <div><h2 className="h-lg"><Ed value={sec.props.heading} edit={edit} sectionId={sec.id} field="heading" /></h2>
            <dl>
              {(sec.props.addr || edit) && <div><dt>Address</dt><dd><Ed value={sec.props.addr} edit={edit} sectionId={sec.id} field="addr" /></dd></div>}
              {(sec.props.mail || edit) && <div><dt>Email</dt><dd>{edit ? <Ed value={sec.props.mail} edit={edit} sectionId={sec.id} field="mail" /> : <a href={`mailto:${sec.props.mail}`}>{sec.props.mail}</a>}</dd></div>}
              {(sec.props.tel || edit) && <div><dt>Phone</dt><dd>{edit ? <Ed value={sec.props.tel} edit={edit} sectionId={sec.id} field="tel" /> : <a href={`tel:${(sec.props.tel ?? "").replace(/[^\d+]/g, "")}`}>{sec.props.tel}</a>}</dd></div>}
            </dl></div>
          <Photo src={sec.imgs.map} width={900} edit={edit} sectionId={sec.id} slot="map" />
        </div></div>
      );
    case "services": {
      const list = (data.services ?? []).slice(0, n);
      const rows = v === "list";
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          {rows ? (
            <div className="rowlist">{list.map((x) => (
              <article className="link" key={x.id}><div><h3>{x.name}</h3>{x.description && <p>{x.description}</p>}</div><Price v={x.price > 0 ? money(x.price, cur) : "Free"} />
                <Go ctx={ctx} href={portalPath.services(x.id)} className="btn sm">Get this{ARW}</Go></article>
            ))}</div>
          ) : (
            <div className="bal cards" style={{ ["--n" as string]: list.length }}>{list.map((x, i) => (
              <article className="card link" key={x.id}><div className="b">{icon(i)}<h3>{x.name}</h3>{x.description && <p>{x.description}</p>}
                <div className="cfoot"><Price v={x.price > 0 ? money(x.price, cur) : "Free"} /><Go ctx={ctx} href={portalPath.services(x.id)} className="btn sm">Get this{ARW}</Go></div></div></article>
            ))}</div>
          )}
        </div>
      );
    }
    case "store": {
      const list = (data.store ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          <div className="bal cards prod" style={{ ["--n" as string]: list.length }}>{list.map((x) => (
            <article className="card link" key={x.id}>
              <Go ctx={ctx} href={portalPath.store(x.id)} className="pimg" title={`View ${x.name}`}>
                {x.imageUrl ? <Photo src={x.imageUrl} alt={x.name} width={700} className="" /> : <span className="noimg" aria-hidden="true">{initials(x.name)}</span>}
                <span className="quick">View details{ARW}</span>
              </Go>
              <div className="b">{x.category && <span className="cat">{x.category}</span>}<h3>{x.name}</h3>
                <div className="cfoot"><Price v={money(x.price, cur)} /><Go ctx={ctx} href={portalPath.store(x.id)} className="btn sm">{BAG}Add</Go></div></div>
            </article>
          ))}</div>
        </div>
      );
    }
    case "fundraisers": {
      const list = (data.fundraisers ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          <div className="bal cards" style={{ ["--n" as string]: list.length }}>{list.map((x) => {
            const p = pct(x.raised, x.goal);
            return (
              <article className="card link" key={x.id}><div className="b"><h3>{x.title}</h3>{x.summary && <p>{x.summary}</p>}
                <div className="bar" role="img" aria-label={`${p}% raised`}><i style={{ width: `${p}%` }} /></div>
                <div className="fmeta"><span><b>{money(x.raised, cur)}</b> <small>raised of {money(x.goal, cur)}</small></span><em>{p}%</em></div>
                <div className="cfoot"><Go ctx={ctx} href={portalPath.fundraisers(x.id)} className="btn sm block">Give now{ARW}</Go></div></div></article>
            );
          })}</div>
        </div>
      );
    }
    case "jobs": {
      const list = (data.jobs ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          <div className="rowlist">{list.map((x) => (
            <article className="link" key={x.id}><div><h3>{x.title}</h3><p>{[x.company, x.location].filter(Boolean).join(" · ")}</p></div><span />
              <Go ctx={ctx} href={portalPath.jobs(x.id)} className="btn sm ghost">View{ARW}</Go></article>
          ))}</div>
        </div>
      );
    }
    case "businesses": {
      const list = (data.businesses ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          <div className="bal cards" style={{ ["--n" as string]: list.length }}>{list.map((x, i) => (
            <article className="card link" key={x.id}><div className="b">{icon(i + 2)}<h3>{x.name}</h3>
              <div className="cfoot">{x.category ? <span className="chip">{x.category}</span> : <span />}<Go ctx={ctx} href={portalPath.businesses(x.id)} className="btn sm ghost">Visit{ARW}</Go></div></div></article>
          ))}</div>
        </div>
      );
    }
    case "spotlights": {
      const list = (data.spotlights ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          <div className="bal cards" style={{ ["--n" as string]: list.length }}>{list.map((x) => (
            <article className="card link" key={x.id}>
              {x.imageUrl ? <Photo src={x.imageUrl} alt={x.memberName ?? ""} width={700} className="newsimg" /> : <div className="img newsimg"><span className="noimg" aria-hidden="true" style={{ position: "absolute", inset: 0, display: "grid", placeItems: "center", font: "600 48px var(--fd)", color: "var(--p)" }}>{initials(x.memberName ?? x.title)}</span></div>}
              <div className="b">
                {x.memberName && <span className="cat">{x.memberName}</span>}
                <h3>{x.title}</h3>{x.story && <p className="clamp3">{x.story}</p>}
                <Go ctx={ctx} href={portalPath.spotlights} className="more">Read the story{ARW}</Go>
              </div>
            </article>
          ))}</div>
        </div>
      );
    }
    case "communities": {
      const list = (data.communities ?? []).slice(0, n);
      return (
        <div className="wrap"><SecHead sec={sec} ctx={ctx} />
          <div className="bal cards" style={{ ["--n" as string]: list.length }}>{list.map((x) => (
            <article className="card link" key={x.id}><div className="b">{icon(4)}<h3>{x.name}</h3>
              <div className="cfoot">{x.memberCount != null ? <Price v={group(x.memberCount)} small="Members" /> : <span />}<Go ctx={ctx} href={portalPath.communities(x.id)} className="btn sm ghost">Join{ARW}</Go></div></div></article>
          ))}</div>
        </div>
      );
    }
  }
}

function AddItem({ sec, ctx }: P) {
  const b = BLOCKS[sec.type];
  if (!ctx.edit || !b.items) return null;
  return <div className="itadd"><button type="button" className="btn ghost" onClick={(e) => { e.stopPropagation(); ctx.edit!.addItem(sec.id); }}>Add {sec.type === "gallery" ? "a photo" : "a " + b.items.word}</button></div>;
}

function Cards({ sec, ctx }: P) {
  const { edit } = ctx, v = sec.variant, its = sec.items ?? [];
  const remove = (id: string, label: string) => edit && <button type="button" className="itx" onClick={(e) => { e.stopPropagation(); edit.removeItem(sec.id, id); }}>{label}</button>;
  const body = (it: SiteItem, i: number) => (
    <>
      {v === "numbered" && <span className="num">{String(i + 1).padStart(2, "0")}</span>}
      <h3><Ed value={it.fields.title} edit={edit} sectionId={sec.id} itemId={it.id} field="title" placeholder="Title" /></h3>
      <div className="ctext"><Rich value={it.fields.text} edit={edit} sectionId={sec.id} itemId={it.id} field="text" /></div>
      {(it.fields.btn || edit) && <div className="btns" style={{ marginTop: v === "steps" ? 18 : 14 }}><Btn sec={sec} ctx={ctx} item={it} k="btn" cls="ghost" label={it.fields.btn ?? ""} /></div>}
    </>
  );
  return (
    <div className="wrap"><SecHead sec={sec} ctx={ctx} />
      {its.length ? v === "steps" ? (
        <div className="steps">{its.map((it, i) => (
          <article className={`step ${i % 2 ? "flip" : ""}`} key={it.id}>
            <div className="stepcard"><span className="pill">STEP {i + 1} OF {its.length}</span>{body(it, i)}{remove(it.id, "Remove this step")}</div>
            <Photo src={sec.imgs["i" + it.id]} width={700} className="stepfig" edit={edit} sectionId={sec.id} slot={"i" + it.id} />
          </article>
        ))}</div>
      ) : (
        <div className="bal cards" style={{ ["--n" as string]: its.length }}>{its.map((it, i) => (
          <article className="card" key={it.id}>
            {v === "photo" && <Photo src={sec.imgs["i" + it.id]} width={700} className="cardimg" edit={edit} sectionId={sec.id} slot={"i" + it.id} />}
            <div className="b">{body(it, i)}{remove(it.id, "Remove this card")}</div>
          </article>
        ))}</div>
      ) : edit ? <button type="button" className="emptybox" onClick={(e) => { e.stopPropagation(); edit.addItem(sec.id); }}>Add the first card</button> : null}
      <AddItem sec={sec} ctx={ctx} />
    </div>
  );
}

function Hero({ sec, ctx }: P) {
  const { edit, data } = ctx, v = sec.variant;
  const head = (style?: React.CSSProperties) => (
    <>
      {(sec.props.eyebrow || (edit && sec.props.eyebrow !== undefined)) && <span className="eyebrow"><Ed value={sec.props.eyebrow} edit={edit} sectionId={sec.id} field="eyebrow" placeholder="Label" /></span>}
      <h1 className="h-xl" style={style}>
        <Ed value={sec.props.heading} edit={edit} sectionId={sec.id} field="heading" placeholder="Your headline" />
        {(sec.props.heading2 || (edit && sec.props.heading2 !== undefined)) && <> <em className="acc"><Ed value={sec.props.heading2} edit={edit} sectionId={sec.id} field="heading2" /></em></>}
      </h1>
    </>
  );
  const btns = (
    <div className="btns">
      {(sec.props.b1 || edit) && sec.props.b1 !== "" && <Btn sec={sec} ctx={ctx} k="b1" label={sec.props.b1 ?? ""} />}
      {sec.props.b2 != null && sec.props.b2 !== "" && <Btn sec={sec} ctx={ctx} k="b2" cls="ghost" label={sec.props.b2} />}
    </div>
  );
  if (v === "full") {
    const bg = sec.imgs.bg;
    return (
      <div className="img" data-img="bg">
        {bg && <div className="bgl" style={{ backgroundImage: `url('${bg}')` }} />}
        {edit && <button type="button" className="pick" onClick={(e) => { e.stopPropagation(); edit.pickPhoto(sec.id, "bg"); }}>{bg ? "Replace photo" : "Add a photo"}</button>}
        <div className="content"><div className="wrap">{head({ maxWidth: "16ch" })}<div className="lead"><Rich value={sec.props.body} edit={edit} sectionId={sec.id} field="body" /></div>{btns}</div></div>
      </div>
    );
  }
  if (v === "center") {
    return (
      <>
        <div className="wrap narrow">{head()}<div className="lead"><Rich value={sec.props.body} edit={edit} sectionId={sec.id} field="body" /></div>{btns}</div>
        {(sec.imgs.a || sec.imgs.b || sec.imgs.c || edit) && <div className="wrap"><div className="strip">{(["a", "b", "c"] as const).map((k) => <Photo key={k} src={sec.imgs[k]} width={600} edit={edit} sectionId={sec.id} slot={k} />)}</div></div>}
      </>
    );
  }
  const next = data.events?.[0];
  return (
    <>
      <div className="decos" aria-hidden="true"><i className="deco d1" /><i className="deco d2" /><i className="deco d3" /></div>
      <div className="wrap">
        <div className="hcopy">{head()}<div className="lead"><Rich value={sec.props.body} edit={edit} sectionId={sec.id} field="body" /></div>{btns}</div>
        {(sec.imgs.main || edit) && (
          <div className="hmedia">
            <Photo src={sec.imgs.main} width={900} priority className="herophoto" edit={edit} sectionId={sec.id} slot="main" />
            {next && <div className="hchip"><b>{dayOf(next.startDate)}<small>{monthOf(next.startDate)}</small></b><span>{next.title}</span></div>}
          </div>
        )}
      </div>
    </>
  );
}

export const TYPE_LABEL = (t: SectionType) => BLOCKS[t].name;
