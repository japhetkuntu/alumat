import { platformClient } from './api-client';
export const CHANNELS = ['Facebook','Instagram','LinkedIn','WhatsApp','X','TikTok','YouTube','Other'];
export interface Campaign { id:string; title:string; description:string; status:string; postCount?:number; shareCount?:number; enquiries?:number }
export interface Asset { id:string; name:string; url:string; contentType:string; size:number }
export interface LandingPageStory { audience:string; problem:string; promisedValue:string; proof:string; nextAction:string }
export interface Post { id:string; title:string; caption:string; content:string; landingPage:LandingPageStory; status:string; destinationPath:string; useLandingPage:boolean; captions:Record<string,string>; assetIds:string[] }
export interface Share { id:string; shortCode?:string; postId:string; channel:string; caption:string; snapshotJson:string; createdAt:string; createdBy:string; sharedAt?:string; publishedUrl?:string; disabled:boolean; visits?:number; enquiries?:number }
export interface Detail { campaign:Campaign; posts:Post[]; assets:Asset[]; shares:Share[] }
const emptyLandingPage:LandingPageStory={audience:'',problem:'',promisedValue:'',proof:'',nextAction:''};
function decodePost(post:Omit<Post,'landingPage'>):Post {
 let landingPage={...emptyLandingPage};
 try {
  const parsed=JSON.parse(post.content) as Partial<LandingPageStory>&{version?:number};
  if(parsed.version===1) landingPage={audience:parsed.audience||'',problem:parsed.problem||'',promisedValue:parsed.promisedValue||'',proof:parsed.proof||'',nextAction:parsed.nextAction||''};
  else landingPage.promisedValue=post.content;
 } catch { landingPage.promisedValue=post.content; }
 return {...post,landingPage};
}
function encodePost(post:Post) {
 const {landingPage,...request}=post;
 return {...request,content:JSON.stringify({version:1,...landingPage})};
}
export const marketingApi = {
 list: async()=> (await platformClient.get<Campaign[]>('/marketing-campaigns')).data,
 detail: async(id:string)=> {const detail=(await platformClient.get<Omit<Detail,'posts'>&{posts:Omit<Post,'landingPage'>[]}>(`/marketing-campaigns/${id}`)).data;return {...detail,posts:detail.posts.map(decodePost)};},
 create: async(data:Partial<Campaign>)=> (await platformClient.post<Campaign>('/marketing-campaigns',data)).data,
 update: async(id:string,data:Partial<Campaign>)=> (await platformClient.put(`/marketing-campaigns/${id}`,data)).data,
 savePost: async(id:string,p:Post)=> decodePost((await platformClient.put<Omit<Post,'landingPage'>>(`/marketing-campaigns/${id}/posts/${p.id || 'new'}`,encodePost(p))).data),
 upload: async(id:string,file:File)=> {const form=new FormData();form.append('file',file);return (await platformClient.post<Asset>(`/marketing-campaigns/${id}/assets`,form,{timeout:180000})).data;},
 deleteAsset: async(id:string,assetId:string)=> platformClient.delete(`/marketing-campaigns/${id}/assets/${assetId}`),
 deletePost: async(id:string,postId:string)=> platformClient.delete(`/marketing-campaigns/${id}/posts/${postId}`),
 prepare: async(id:string,postId:string,channel:string)=> (await platformClient.post<Share>(`/marketing-campaigns/${id}/posts/${postId}/shares`,{channel})).data,
 confirm: async(id:string,s:Share,publishedUrl:string)=> platformClient.post(`/marketing-campaigns/${id}/shares/${s.id}/confirm`,{publishedUrl}),
 link: async(id:string,s:Share)=> platformClient.put(`/marketing-campaigns/${id}/shares/${s.id}/link`,{disabled:!s.disabled}),
 deleteShare: async(id:string,shareId:string)=> platformClient.delete(`/marketing-campaigns/${id}/shares/${shareId}`),
};
// Prefers the short code (new shares always have one) over the full Id — see
// MarketingRules.GenerateShortCode. Older shares without one fall back to their Id.
export function shareUrl(s:{id:string;shortCode?:string}) { return `${(process.env.NEXT_PUBLIC_MARKETING_URL || (process.env.NODE_ENV === 'development' ? 'http://localhost:3200' : 'https://alumunion.com')).replace(/\/$/,'')}/campaigns/${s.shortCode||s.id}`; }
