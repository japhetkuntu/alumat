/**
 * Country + dial-code list for phone number entry. Ghana is first since this
 * platform is Ghana-based (Arkesel SMS, GHS currency) — every other country
 * follows alphabetically for the diaspora members who live abroad.
 */
export interface CountryDialCode {
  name: string;
  /** ISO 3166-1 alpha-2, used to compute the flag emoji at render time. */
  iso2: string;
  /** Without the leading "+". */
  dialCode: string;
}

export const COUNTRY_DIAL_CODES: CountryDialCode[] = [
  { name: "Ghana", iso2: "GH", dialCode: "233" },
  { name: "Afghanistan", iso2: "AF", dialCode: "93" },
  { name: "Albania", iso2: "AL", dialCode: "355" },
  { name: "Algeria", iso2: "DZ", dialCode: "213" },
  { name: "Argentina", iso2: "AR", dialCode: "54" },
  { name: "Australia", iso2: "AU", dialCode: "61" },
  { name: "Austria", iso2: "AT", dialCode: "43" },
  { name: "Bahrain", iso2: "BH", dialCode: "973" },
  { name: "Bangladesh", iso2: "BD", dialCode: "880" },
  { name: "Belgium", iso2: "BE", dialCode: "32" },
  { name: "Benin", iso2: "BJ", dialCode: "229" },
  { name: "Botswana", iso2: "BW", dialCode: "267" },
  { name: "Brazil", iso2: "BR", dialCode: "55" },
  { name: "Burkina Faso", iso2: "BF", dialCode: "226" },
  { name: "Burundi", iso2: "BI", dialCode: "257" },
  { name: "Cameroon", iso2: "CM", dialCode: "237" },
  { name: "Canada", iso2: "CA", dialCode: "1" },
  { name: "Cape Verde", iso2: "CV", dialCode: "238" },
  { name: "Central African Republic", iso2: "CF", dialCode: "236" },
  { name: "Chad", iso2: "TD", dialCode: "235" },
  { name: "Chile", iso2: "CL", dialCode: "56" },
  { name: "China", iso2: "CN", dialCode: "86" },
  { name: "Colombia", iso2: "CO", dialCode: "57" },
  { name: "Congo (DRC)", iso2: "CD", dialCode: "243" },
  { name: "Congo (Republic)", iso2: "CG", dialCode: "242" },
  { name: "Côte d'Ivoire", iso2: "CI", dialCode: "225" },
  { name: "Denmark", iso2: "DK", dialCode: "45" },
  { name: "Djibouti", iso2: "DJ", dialCode: "253" },
  { name: "Egypt", iso2: "EG", dialCode: "20" },
  { name: "Equatorial Guinea", iso2: "GQ", dialCode: "240" },
  { name: "Eritrea", iso2: "ER", dialCode: "291" },
  { name: "Eswatini", iso2: "SZ", dialCode: "268" },
  { name: "Ethiopia", iso2: "ET", dialCode: "251" },
  { name: "Finland", iso2: "FI", dialCode: "358" },
  { name: "France", iso2: "FR", dialCode: "33" },
  { name: "Gabon", iso2: "GA", dialCode: "241" },
  { name: "Gambia", iso2: "GM", dialCode: "220" },
  { name: "Germany", iso2: "DE", dialCode: "49" },
  { name: "Greece", iso2: "GR", dialCode: "30" },
  { name: "Guinea", iso2: "GN", dialCode: "224" },
  { name: "Guinea-Bissau", iso2: "GW", dialCode: "245" },
  { name: "India", iso2: "IN", dialCode: "91" },
  { name: "Indonesia", iso2: "ID", dialCode: "62" },
  { name: "Iran", iso2: "IR", dialCode: "98" },
  { name: "Iraq", iso2: "IQ", dialCode: "964" },
  { name: "Ireland", iso2: "IE", dialCode: "353" },
  { name: "Israel", iso2: "IL", dialCode: "972" },
  { name: "Italy", iso2: "IT", dialCode: "39" },
  { name: "Jamaica", iso2: "JM", dialCode: "1876" },
  { name: "Japan", iso2: "JP", dialCode: "81" },
  { name: "Jordan", iso2: "JO", dialCode: "962" },
  { name: "Kenya", iso2: "KE", dialCode: "254" },
  { name: "Kuwait", iso2: "KW", dialCode: "965" },
  { name: "Lebanon", iso2: "LB", dialCode: "961" },
  { name: "Lesotho", iso2: "LS", dialCode: "266" },
  { name: "Liberia", iso2: "LR", dialCode: "231" },
  { name: "Libya", iso2: "LY", dialCode: "218" },
  { name: "Madagascar", iso2: "MG", dialCode: "261" },
  { name: "Malawi", iso2: "MW", dialCode: "265" },
  { name: "Malaysia", iso2: "MY", dialCode: "60" },
  { name: "Mali", iso2: "ML", dialCode: "223" },
  { name: "Mauritania", iso2: "MR", dialCode: "222" },
  { name: "Mauritius", iso2: "MU", dialCode: "230" },
  { name: "Mexico", iso2: "MX", dialCode: "52" },
  { name: "Morocco", iso2: "MA", dialCode: "212" },
  { name: "Mozambique", iso2: "MZ", dialCode: "258" },
  { name: "Namibia", iso2: "NA", dialCode: "264" },
  { name: "Netherlands", iso2: "NL", dialCode: "31" },
  { name: "New Zealand", iso2: "NZ", dialCode: "64" },
  { name: "Niger", iso2: "NE", dialCode: "227" },
  { name: "Nigeria", iso2: "NG", dialCode: "234" },
  { name: "Norway", iso2: "NO", dialCode: "47" },
  { name: "Oman", iso2: "OM", dialCode: "968" },
  { name: "Pakistan", iso2: "PK", dialCode: "92" },
  { name: "Philippines", iso2: "PH", dialCode: "63" },
  { name: "Poland", iso2: "PL", dialCode: "48" },
  { name: "Portugal", iso2: "PT", dialCode: "351" },
  { name: "Qatar", iso2: "QA", dialCode: "974" },
  { name: "Rwanda", iso2: "RW", dialCode: "250" },
  { name: "Saudi Arabia", iso2: "SA", dialCode: "966" },
  { name: "Senegal", iso2: "SN", dialCode: "221" },
  { name: "Sierra Leone", iso2: "SL", dialCode: "232" },
  { name: "Singapore", iso2: "SG", dialCode: "65" },
  { name: "Somalia", iso2: "SO", dialCode: "252" },
  { name: "South Africa", iso2: "ZA", dialCode: "27" },
  { name: "South Korea", iso2: "KR", dialCode: "82" },
  { name: "South Sudan", iso2: "SS", dialCode: "211" },
  { name: "Spain", iso2: "ES", dialCode: "34" },
  { name: "Sri Lanka", iso2: "LK", dialCode: "94" },
  { name: "Sudan", iso2: "SD", dialCode: "249" },
  { name: "Sweden", iso2: "SE", dialCode: "46" },
  { name: "Switzerland", iso2: "CH", dialCode: "41" },
  { name: "Tanzania", iso2: "TZ", dialCode: "255" },
  { name: "Thailand", iso2: "TH", dialCode: "66" },
  { name: "Togo", iso2: "TG", dialCode: "228" },
  { name: "Tunisia", iso2: "TN", dialCode: "216" },
  { name: "Turkey", iso2: "TR", dialCode: "90" },
  { name: "Uganda", iso2: "UG", dialCode: "256" },
  { name: "Ukraine", iso2: "UA", dialCode: "380" },
  { name: "United Arab Emirates", iso2: "AE", dialCode: "971" },
  { name: "United Kingdom", iso2: "GB", dialCode: "44" },
  { name: "United States", iso2: "US", dialCode: "1" },
  { name: "Zambia", iso2: "ZM", dialCode: "260" },
  { name: "Zimbabwe", iso2: "ZW", dialCode: "263" },
];

/** Regional-indicator flag emoji computed from an ISO 3166-1 alpha-2 code — no need to hand-maintain 100+ emoji strings. */
export function flagEmoji(iso2: string): string {
  return String.fromCodePoint(...[...iso2.toUpperCase()].map((c) => 127397 + c.charCodeAt(0)));
}

export const DEFAULT_COUNTRY_ISO2 = "GH";

/** Longest-dial-code-first so e.g. "234..." matches Nigeria (234) before a shorter prefix could misfire. */
export const COUNTRY_DIAL_CODES_BY_LENGTH = [...COUNTRY_DIAL_CODES].sort(
  (a, b) => b.dialCode.length - a.dialCode.length
);
