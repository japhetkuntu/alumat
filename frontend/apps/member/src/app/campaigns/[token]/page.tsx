import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import Link from 'next/link';
import { CampaignArrival } from './tracking';
import { ZoomableImage } from '@alumni/ui';

interface PublicCampaign {id:string;title:string;content:string;caption:string;campaignTitle:string;channel:string;destinationPath:string;useLandingPage:boolean;assets:{id:string;url:string;name:string;contentType:string}[]}
interface LandingPageStory {audience:string;problem:string;promisedValue:string;proof:string;nextAction:string}

function landingPageStory(content:string,caption:string):LandingPageStory{
  try {
    const parsed=JSON.parse(content) as Partial<LandingPageStory>&{version?:number};
    if(parsed.version===1)return {audience:parsed.audience||'',problem:parsed.problem||'',promisedValue:parsed.promisedValue||'',proof:parsed.proof||'',nextAction:parsed.nextAction||''};
  } catch { /* Existing campaigns contain plain text. */ }
  return {audience:'',problem:'',promisedValue:content||caption,proof:'',nextAction:''};
}

// Accepts either a share's full 32-char hex Id (old links) or its short ShortCode
// (new links — see MarketingRules.GenerateShortCode); the API resolves either.
function isValidToken(token:string){return /^[a-f0-9]{32}$/.test(token)||/^[2-9a-zA-Z]{6,14}$/.test(token);}

async function campaign(token:string):Promise<PublicCampaign|null>{
  if(!isValidToken(token)) return null;
  const res=await fetch(`${process.env.MEMBER_API_INTERNAL_URL||'http://localhost:5200/api/v1'}/public/marketing/${token}`,{cache:'no-store',signal:AbortSignal.timeout(10000)});
  if(res.status===404) return null;
  if(!res.ok) throw new Error('Campaign unavailable. Please try again.');
  return res.json();
}

export async function generateMetadata({params}:{params:Promise<{token:string}>}):Promise<Metadata>{
  const {token}=await params;
  const c=await campaign(token);
  if(!c) return {title:'Campaign unavailable'};
  const image=c.assets.find(a=>a.contentType.startsWith('image/'));
  const story=landingPageStory(c.content,c.caption);
  const description=(story.problem||story.promisedValue||c.caption).slice(0,180);
  return {
    title:`${c.title} | AlumUnion`,
    description,
    robots:{index:false,follow:true},
    openGraph:{title:c.title,description,images:image?[{url:image.url}]:[]},
    twitter:{card:'summary_large_image',title:c.title,images:image?[image.url]:[]},
  };
}

export default async function Page({params}:{params:Promise<{token:string}>}){
  const {token}=await params;
  const c=await campaign(token);
  if(!c) notFound();

  // Always attribute by the share's real Id (c.id), never the raw path token — a visitor
  // arriving via the short link must still produce the same mc/sessionStorage value as one
  // arriving via the full link, so lead attribution (OnboardingLead.MarketingShareId) and the
  // campaign detail's enquiries count keep matching against MarketingShare.Id either way.
  const destination=new URL(c.destinationPath,'https://alumunion.com');
  destination.searchParams.set('mc',c.id);
  destination.searchParams.set('utm_source',c.channel.toLowerCase());
  destination.searchParams.set('utm_medium','social');
  destination.searchParams.set('utm_campaign',c.campaignTitle);
  destination.searchParams.set('utm_content',c.id);
  const target=destination.pathname+destination.search+destination.hash;
  const story=landingPageStory(c.content,c.caption);
  const defaultCtaLabel=c.destinationPath==='/#onboard' ? 'Request a walkthrough'
    : c.destinationPath==='/#features' ? 'See what members can do'
    : c.destinationPath==='/#costs' ? 'View plans'
    : c.destinationPath==='/why-not-whatsapp' ? 'Compare the experience'
    : 'Explore AlumUnion';
  const ctaLabel=story.nextAction||defaultCtaLabel;

  const [hero,...rest]=c.assets;
  const introduction=story.problem||story.promisedValue;

  return (
    <main className="min-h-screen bg-background cp-page">
      <CampaignArrival token={c.id} redirectTo={c.useLandingPage?undefined:target}/>

      <header className="border-b border-border">
        <div className="section__inner--wide flex items-center justify-between py-5">
          <Link href="/" aria-label="AlumUnion home">
            <img src="/alumunion-logo-horizontal.svg" alt="AlumUnion" width={1490} height={405} className="h-9 w-auto" />
          </Link>
          <span className="hidden sm:block text-[13px] text-muted-foreground">A community platform for institutions, members and supporters</span>
        </div>
      </header>

      <section className="section">
        <div className="section__inner--wide">
          <div className="grid lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] gap-10 lg:gap-16 items-center">

            {hero && (
              <div className="cp-in space-y-4 order-2 lg:order-1">
                <div className="relative w-full bg-muted overflow-hidden" style={{aspectRatio:'4/5'}}>
                  {hero.contentType.startsWith('video/') ? (
                    <video src={hero.url} controls playsInline preload="metadata" className="absolute inset-0 h-full w-full object-contain" />
                  ) : (
                    <ZoomableImage src={hero.url} alt={hero.name} wrapperClassName="absolute inset-0" className="h-full w-full object-contain" />
                  )}
                </div>
                {rest.length > 0 && (
                  <div className="grid grid-cols-3 gap-3">
                    {rest.slice(0,3).map(a=> (
                      <div key={a.id} className="relative bg-muted overflow-hidden" style={{aspectRatio:'4/5'}}>
                        {a.contentType.startsWith('video/') ? (
                          <video src={a.url} playsInline preload="metadata" className="absolute inset-0 h-full w-full object-contain" />
                        ) : (
                          <ZoomableImage src={a.url} alt={a.name} wrapperClassName="absolute inset-0" className="h-full w-full object-contain" />
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            <div className={`space-y-6 ${hero ? 'order-1 lg:order-2' : ''}`}>
              <p className="cp-in cp-d1 text-[13px] font-bold tracking-[0.12em] uppercase" style={{color:'var(--primary)'}}>{story.audience||c.campaignTitle}</p>
              <h1 className="cp-in cp-d2 text-3xl sm:text-4xl lg:text-[2.75rem] font-bold leading-[1.1] tracking-tight text-balance">{c.title}</h1>
              <p className="cp-in cp-d3 text-base sm:text-lg leading-relaxed text-muted-foreground max-w-[58ch] whitespace-pre-wrap">{introduction}</p>
              <div className="cp-in cp-d3 flex flex-wrap items-center gap-4">
                <a href={target} className="btn btn-primary inline-flex items-center gap-2">
                  {ctaLabel}
                  <span aria-hidden="true">&rarr;</span>
                </a>
                <span className="text-xs text-muted-foreground">No commitment required to explore</span>
              </div>
            </div>

          </div>
        </div>
      </section>

      <section className="border-y border-border bg-card/40">
        <div className="section__inner--wide py-12 sm:py-16">
          <div className="max-w-3xl">
            <p className="text-xs font-bold uppercase tracking-[0.12em]" style={{color:'var(--primary)'}}>What you came for</p>
            <h2 className="mt-3 text-2xl sm:text-3xl font-bold">Useful before you take the next step.</h2>
            <p className="mt-5 whitespace-pre-wrap text-base sm:text-lg leading-relaxed text-muted-foreground">{story.promisedValue}</p>
          </div>
          {story.proof&&<div className="mt-8 max-w-3xl border-l-4 pl-5 py-1" style={{borderColor:'var(--primary)'}}><p className="text-xs font-bold uppercase tracking-[0.12em] text-muted-foreground">Why believe it</p><p className="mt-2 whitespace-pre-wrap leading-relaxed">{story.proof}</p></div>}
        </div>
      </section>

      <section className="section">
        <div className="section__inner--wide">
          <div className="rounded-2xl border border-border bg-card p-6 sm:p-9 flex flex-col sm:flex-row sm:items-center justify-between gap-6">
            <div><p className="text-xs font-bold uppercase tracking-[0.12em]" style={{color:'var(--primary)'}}>Your next step</p><h2 className="mt-2 text-2xl font-bold">Ready to continue?</h2><p className="mt-2 text-sm text-muted-foreground max-w-2xl">Take the next action when it is useful to you.</p></div>
            <a href={target} className="btn btn-primary inline-flex shrink-0 items-center justify-center gap-2">{ctaLabel}<span aria-hidden="true">&rarr;</span></a>
          </div>
        </div>
      </section>

      <div className="fixed inset-x-0 bottom-0 z-20 border-t border-border bg-background/95 p-3 backdrop-blur sm:hidden">
        <a href={target} className="btn btn-primary flex w-full items-center justify-center gap-2">{ctaLabel}<span aria-hidden="true">&rarr;</span></a>
      </div>

      <style>{`
        .cp-page { padding-bottom: 64px; }
        .cp-in { opacity: 0; animation: cp-fade-up .6s cubic-bezier(.16,1,.3,1) both; }
        .cp-d1 { animation-delay: .05s; }
        .cp-d2 { animation-delay: .12s; }
        .cp-d3 { animation-delay: .2s; }
        @keyframes cp-fade-up {
          from { opacity: 0; transform: translateY(14px); }
          to { opacity: 1; transform: none; }
        }
        @media (prefers-reduced-motion: reduce) {
          .cp-in { animation: none; opacity: 1; transform: none; }
        }
        @media (min-width: 640px) { .cp-page { padding-bottom: 0; } }
      `}</style>
    </main>
  );
}
