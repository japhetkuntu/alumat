"use client";
import { useEffect } from 'react';
import { usePathname } from 'next/navigation';
import { publicMemberClient } from '@/lib/api-client';
import { rememberMarketingShare } from '@/lib/marketing-attribution';
export async function recordMarketingVisit(token:string){
 if(!/^[a-f0-9]{32}$/.test(token))return;
 try {
  const session=sessionStorage.getItem('marketing-session')||crypto.randomUUID();
  sessionStorage.setItem('marketing-session',session);
  await publicMemberClient.post(`/public/marketing/${token}/visits`,{sessionId:session});
  rememberMarketingShare(token);
 }catch{/* Analytics failures must not block the website. */}
}
export function MarketingAttribution(){
 const path=usePathname();
 useEffect(()=>{const token=new URLSearchParams(window.location.search).get('mc');if(token)void recordMarketingVisit(token);},[path]);
 return null;
}
