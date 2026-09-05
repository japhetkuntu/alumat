"use client";

import * as React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";

import {
  faWaveSquare,
  faCircleExclamation,
  faTriangleExclamation,
  faAlignCenter,
  faAlignLeft,
  faAlignRight,
  faBoxArchive,
  faBoxOpen,
  faChair,
  faArrowDown,
  faArrowLeft,
  faArrowRight,
  faArrowUp,
  faAward,
  faBan,
  faChartBar,
  faBold,
  faBookOpen,
  faBriefcase,
  faBuilding,
  faCamera,
  faCheck,
  faCheckDouble,
  faChevronDown,
  faChevronLeft,
  faChevronRight,
  faChevronUp,
  faClipboardList,
  faCode,
  faCreditCard,
  faCrown,
  faDollarSign,
  faDownload,
  faExpand,
  faArrowUpRightFromSquare,
  faCircleInfo,
  faGlobe,
  faGraduationCap,
  faHandHoldingDollar,
  faHeading,
  faHandHoldingHeart,
  faImage,
  faImages,
  faInbox,
  faItalic,
  faLandmark,
  faLayerGroup,
  faTableCellsLarge,
  faLifeRing,
  faLink,
  faList,
  faListOl,
  faSpinner,
  faLock,
  faEnvelopeCircleCheck,
  faLocationDot,
  faMedal,
  faBullhorn,
  faBars,
  faCommentSlash,
  faMinus,
  faArrowsLeftRight,
  faLocationArrow,
  faNewspaper,
  faBox,
  faChampagneGlasses,
  faPen,
  faPhone,
  faPhoneSlash,
  faThumbtack,
  faPlay,
  faPlus,
  faQuoteLeft,
  faTowerBroadcast,
  faRotateRight,
  faArrowsRotate,
  faRocket,
  faMagnifyingGlass,
  faPaperPlane,
  faGear,
  faSliders,
  faShareNodes,
  faShield,
  faShieldHalved,
  faBagShopping,
  faCartShopping,
  faMobileScreen,
  faWandMagicSparkles,
  faStore,
  faStrikethrough,
  faBullseye,
  faTicket,
  faTrash,
  faArrowTrendUp,
  faTrophy,
  faTruck,
  faUnderline,
  faRotateLeft,
  faUpload,
  faUserCheck,
  faUserMinus,
  faUserXmark,
  faUsers,
  faUserGroup,
  faWallet,
  faXmark,
  faBolt,
  faMagnifyingGlassPlus,
} from "@fortawesome/free-solid-svg-icons";

import {
  faBell,
  faBookmark,
  faCalendar,
  faCalendarCheck,
  faCalendarDays,
  faCircleCheck,
  faCircle,
  faClock,
  faCopy,
  faEye,
  faEyeSlash,
  faFile,
  faFileLines,
  faFolderOpen,
  faHeart,
  faCommentDots,
  faMessage,
  faStar,
  faUser,
  faCircleUser,
  faCircleXmark,
  faEnvelope,
} from "@fortawesome/free-regular-svg-icons";

import {
  faFacebook,
  faLinkedin,
  faXTwitter,
  faYoutube,
} from "@fortawesome/free-brands-svg-icons";

/* ─────────────────────────────────────────────────────────────────────────
   LUCIDE → FONT AWESOME COMPATIBILITY LAYER

   Every icon component below has the exact export name of the lucide-react
   icon it replaces, and accepts the same call-site shape (`size`, `color`,
   `className`, `style`, `strokeWidth`) — so every existing `<X size={16} />`
   usage across the three apps kept working unchanged; only each file's
   `from "lucide-react"` import was repointed to `from "@alumni/ui"`.

   FontAwesome's free tier has no stroke concept (lucide is stroke-based,
   FA is filled), so `strokeWidth` is accepted for API compatibility and
   silently ignored. Where a "regular" (thinner, closer to lucide's look)
   variant exists in the free tier it's preferred over "solid"; several
   lucide icons (party-popper, sparkles, mail-x, search-x, shield-check,
   bookmark-check) have no exact Font Awesome Free equivalent and use the
   closest available icon instead.
   ───────────────────────────────────────────────────────────────────────── */

/** Replaces lucide-react's `LucideIcon` type for typing an icon-component
 *  field (e.g. `{ icon: IconType }[]` feature-list data) now that icons are
 *  Font Awesome-backed components instead. */
export type IconType = React.ComponentType<IconProps>;

export interface IconProps extends Omit<React.SVGAttributes<SVGSVGElement>, "color" | "mask"> {
  size?: number | string;
  color?: string;
  /** Accepted for lucide-react API compatibility; Font Awesome icons have no
   *  stroke, so this has no visual effect. */
  strokeWidth?: number;
}

function makeIcon(definition: IconDefinition) {
  const Icon = React.forwardRef<SVGSVGElement, IconProps>(
    ({ size = 24, color, style, strokeWidth: _strokeWidth, ...rest }, ref) => (
      <FontAwesomeIcon
        icon={definition}
        ref={ref as never}
        style={{ width: size, height: size, ...(color ? { color } : {}), ...style }}
        {...rest}
      />
    )
  );
  Icon.displayName = `Icon(${definition.iconName})`;
  return Icon;
}

export const Activity = makeIcon(faWaveSquare);
export const AlertCircle = makeIcon(faCircleExclamation);
export const AlertTriangle = makeIcon(faTriangleExclamation);
export const AlignCenter = makeIcon(faAlignCenter);
export const AlignLeft = makeIcon(faAlignLeft);
export const AlignRight = makeIcon(faAlignRight);
export const Archive = makeIcon(faBoxArchive);
export const ArchiveRestore = makeIcon(faBoxOpen);
export const Armchair = makeIcon(faChair);
export const ArrowDown = makeIcon(faArrowDown);
export const ArrowLeft = makeIcon(faArrowLeft);
export const ArrowRight = makeIcon(faArrowRight);
export const ArrowUp = makeIcon(faArrowUp);
export const Award = makeIcon(faAward);
export const Ban = makeIcon(faBan);
export const BarChart3 = makeIcon(faChartBar);
export const Bell = makeIcon(faBell);
export const Bold = makeIcon(faBold);
export const BookOpen = makeIcon(faBookOpen);
export const Bookmark = makeIcon(faBookmark);
export const BookmarkCheck = makeIcon(faBookmark);
export const Briefcase = makeIcon(faBriefcase);
export const Building2 = makeIcon(faBuilding);
export const Calendar = makeIcon(faCalendar);
export const CalendarCheck = makeIcon(faCalendarCheck);
export const CalendarDays = makeIcon(faCalendarDays);
export const CalendarDay = makeIcon(faCalendarDays);
export const Camera = makeIcon(faCamera);
export const Check = makeIcon(faCheck);
export const CheckCheck = makeIcon(faCheckDouble);
export const CheckCircle = makeIcon(faCircleCheck);
export const CheckCircle2 = makeIcon(faCircleCheck);
export const ChevronDown = makeIcon(faChevronDown);
export const ChevronLeft = makeIcon(faChevronLeft);
export const ChevronRight = makeIcon(faChevronRight);
export const ChevronUp = makeIcon(faChevronUp);
export const Circle = makeIcon(faCircle);
export const ClipboardList = makeIcon(faClipboardList);
export const Clock = makeIcon(faClock);
export const Clock3 = makeIcon(faClock);
export const Code = makeIcon(faCode);
export const Copy = makeIcon(faCopy);
export const CreditCard = makeIcon(faCreditCard);
export const Crown = makeIcon(faCrown);
export const DollarSign = makeIcon(faDollarSign);
export const Download = makeIcon(faDownload);
export const Expand = makeIcon(faExpand);
export const ExternalLink = makeIcon(faArrowUpRightFromSquare);
export const Eye = makeIcon(faEye);
export const EyeOff = makeIcon(faEyeSlash);
export const Facebook = makeIcon(faFacebook);
export const FileIcon = makeIcon(faFile);
export const FileText = makeIcon(faFileLines);
export const FolderOpen = makeIcon(faFolderOpen);
export const Globe = makeIcon(faGlobe);
export const GraduationCap = makeIcon(faGraduationCap);
export const HandCoins = makeIcon(faHandHoldingDollar);
export const Heading1 = makeIcon(faHeading);
export const Heading2 = makeIcon(faHeading);
export const Heading3 = makeIcon(faHeading);
export const Heart = makeIcon(faHeart);
export const HeartHandshake = makeIcon(faHandHoldingHeart);
export const ImageIcon = makeIcon(faImage);
export const ImageOff = makeIcon(faImage);
export const Images = makeIcon(faImages);
export const Inbox = makeIcon(faInbox);
export const Info = makeIcon(faCircleInfo);
export const Italic = makeIcon(faItalic);
export const Landmark = makeIcon(faLandmark);
export const Layer = makeIcon(faLayerGroup);
export const Layers = makeIcon(faLayerGroup);
export const LayoutDashboard = makeIcon(faTableCellsLarge);
export const LifeBuoy = makeIcon(faLifeRing);
export const Link = makeIcon(faLink);
export const Link2 = makeIcon(faLink);
export const Linkedin = makeIcon(faLinkedin);
export const List = makeIcon(faList);
export const ListOrdered = makeIcon(faListOl);
export const Loader2 = makeIcon(faSpinner);
export const Lock = makeIcon(faLock);
export const Mail = makeIcon(faEnvelope);
export const MailCheck = makeIcon(faEnvelopeCircleCheck);
export const MailX = makeIcon(faEnvelope);
export const MapPin = makeIcon(faLocationDot);
export const Medal = makeIcon(faMedal);
export const Megaphone = makeIcon(faBullhorn);
export const Menu = makeIcon(faBars);
export const MessageCircle = makeIcon(faCommentDots);
export const MessageCircleOff = makeIcon(faCommentSlash);
export const MessageSquare = makeIcon(faMessage);
export const Minus = makeIcon(faMinus);
export const MoveHorizontal = makeIcon(faArrowsLeftRight);
export const Navigation = makeIcon(faLocationArrow);
export const Newspaper = makeIcon(faNewspaper);
export const Package = makeIcon(faBox);
export const PartyPopper = makeIcon(faChampagneGlasses);
export const Pencil = makeIcon(faPen);
export const Phone = makeIcon(faPhone);
export const PhoneOff = makeIcon(faPhoneSlash);
export const Pin = makeIcon(faThumbtack);
export const Play = makeIcon(faPlay);
export const Plus = makeIcon(faPlus);
export const Quote = makeIcon(faQuoteLeft);
export const Radio = makeIcon(faTowerBroadcast);
export const Receipt = makeIcon(faFileLines);
export const Redo = makeIcon(faRotateRight);
export const RefreshCcw = makeIcon(faArrowsRotate);
export const Rocket = makeIcon(faRocket);
export const Search = makeIcon(faMagnifyingGlass);
export const SearchX = makeIcon(faMagnifyingGlass);
export const Send = makeIcon(faPaperPlane);
export const Settings = makeIcon(faGear);
export const Settings2 = makeIcon(faSliders);
export const Share2 = makeIcon(faShareNodes);
export const Shield = makeIcon(faShield);
export const ShieldAlert = makeIcon(faShieldHalved);
export const ShieldBan = makeIcon(faShieldHalved);
export const ShieldCheck = makeIcon(faShieldHalved);
export const ShoppingBag = makeIcon(faBagShopping);
export const ShoppingCart = makeIcon(faCartShopping);
export const SlidersHorizontal = makeIcon(faSliders);
export const Smartphone = makeIcon(faMobileScreen);
export const Sparkles = makeIcon(faWandMagicSparkles);
export const Star = makeIcon(faStar);
export const Store = makeIcon(faStore);
export const Strikethrough = makeIcon(faStrikethrough);
export const Target = makeIcon(faBullseye);
export const Ticket = makeIcon(faTicket);
export const Trash2 = makeIcon(faTrash);
export const TrendingUp = makeIcon(faArrowTrendUp);
export const Trophy = makeIcon(faTrophy);
export const Truck = makeIcon(faTruck);
export const Twitter = makeIcon(faXTwitter);
export const Underline = makeIcon(faUnderline);
export const Undo = makeIcon(faRotateLeft);
export const Upload = makeIcon(faUpload);
export const User = makeIcon(faUser);
export const UserCheck = makeIcon(faUserCheck);
export const UserCircle = makeIcon(faCircleUser);
export const UserMinus = makeIcon(faUserMinus);
export const UserX = makeIcon(faUserXmark);
export const Users = makeIcon(faUsers);
export const Users2 = makeIcon(faUserGroup);
export const UsersRound = makeIcon(faUserGroup);
export const Wallet = makeIcon(faWallet);
export const X = makeIcon(faXmark);
export const XCircle = makeIcon(faCircleXmark);
export const Youtube = makeIcon(faYoutube);
export const Zap = makeIcon(faBolt);
export const ZoomIn = makeIcon(faMagnifyingGlassPlus);
