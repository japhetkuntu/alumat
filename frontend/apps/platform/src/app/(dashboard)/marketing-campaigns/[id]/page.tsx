import { MarketingWorkspace } from '@/components/platform/marketing-workspace';
export default async function Page({params}:{params:Promise<{id:string}>}) {const {id}=await params;return <MarketingWorkspace campaignId={id}/>;}
