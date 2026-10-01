"use client";
import { useEffect } from 'react';
import { recordMarketingVisit } from '@/components/marketing-attribution';
import { rememberMarketingShare } from '@/lib/marketing-attribution';
export function CampaignArrival({token,redirectTo}:{token:string;redirectTo?:string}){
 useEffect(()=>{rememberMarketingShare(token);void recordMarketingVisit(token);if(redirectTo)window.location.replace(redirectTo);},[token,redirectTo]);return null;
}
