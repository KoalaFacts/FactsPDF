import { englishAudit as previous } from './audit-english-corner-longhands.mjs';
// Map unchanged source translations by Chinese source-string identity. Never
// drop/relabel provenance or allow untranslated UI prose into English mode.
if (previous.length !== 722) throw new Error('Previous English audit catalogue changed.');
export const sourceDigest = '652482c24e178daca13927efe4b929df785c10611363aa841240625d14129f78';
const previousIndex = [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,123,124,125,126,127,128,129,130,131,132,133,134,135,136,137,138,139,140,141,142,143,144,145,146,147,148,149,150,151,152,153,154,155,156,157,158,159,160,161,162,163,164,165,166,167,168,-1,-1,-1,-1,-1,-1,-1,174,175,176,177,178,179,180,181,182,183,184,185,186,187,188,189,190,191,192,193,194,195,196,197,198,199,200,201,202,203,204,205,206,207,208,209,210,211,212,213,214,215,216,217,218,219,220,221,222,223,224,225,226,227,228,229,230,231,232,233,234,235,236,237,238,239,240,241,242,243,244,245,246,247,248,249,250,251,252,253,254,255,256,257,258,259,260,261,262,263,264,265,266,267,268,269,270,271,272,273,274,275,276,277,278,279,280,281,282,283,284,285,286,287,288,289,290,291,292,293,294,295,296,-1,-1,-1,-1,-1,-1,-1,297,298,299,300,301,302,303,304,305,306,307,308,309,310,311,312,313,314,315,316,317,318,319,320,321,322,323,324,325,326,327,328,329,330,331,332,333,334,335,336,337,338,339,340,341,342,343,344,345,346,347,348,349,350,351,352,353,354,355,356,357,358,359,360,361,362,363,364,365,366,367,368,369,370,371,372,373,374,375,376,377,378,379,-1,-1,-1,382,383,384,385,386,387,388,389,390,391,392,393,394,395,396,397,398,399,400,401,402,403,404,405,406,407,408,409,410,411,412,413,414,415,416,417,418,419,420,421,422,423,424,425,426,427,428,429,430,431,432,433,434,435,436,437,438,439,440,441,442,443,444,445,446,447,448,449,450,451,452,453,454,455,456,457,458,459,460,461,462,463,464,465,466,467,468,469,470,471,472,473,474,475,476,477,478,479,480,481,482,483,484,485,486,487,488,489,490,491,492,493,494,495,496,497,498,499,500,501,502,503,504,505,506,507,508,509,510,511,512,513,514,515,516,517,518,519,520,521,522,523,524,525,526,527,528,529,530,531,532,533,534,535,536,537,538,539,540,541,542,543,544,545,546,547,548,549,550,551,552,553,554,555,556,557,558,559,560,561,562,563,564,565,566,567,568,569,570,571,572,573,574,575,576,577,578,579,580,581,582,583,584,585,586,587,588,589,590,591,592,593,594,595,596,597,598,599,600,601,602,603,604,605,606,607,608,609,610,611,612,613,614,615,616,617,618,619,620,621,622,623,624,625,626,627,628,629,630,631,632,633,634,635,636,637,638,639,640,641,642,643,644,645,646,647,648,649,650,651,652,653,654,655,656,657,658,659,660,661,662,663,664,665,666,667,668,669,670,671,672,673,674,675,676,677,678,679,680,681,682,683,684,685,686,687,688,689,690,691,692,693,694,695,696,697,698,699,700,701,702,703,704,705,706,707,708,709,710,711,712,713,714,715,716,717,-1,719,720,721];
const additions = {
  "169": "Explicit p/pre/h1–h6 support auto/page; ordinary div/section/article/blockquote report panels now start a real new PDF page before their background, borders and text are laid out.",
  "170": "Only ordinary LTR report panels and explicit paragraphs/headings, not inline/list/table targets or cell paragraphs. No avoid, left/right, column breaks, break-inside or complete CSS fragmentation. FPDF1201/1202 protect original PDF output.",
  "171": "p/pre/h1–h6 outside cells; div/section/article/blockquote outside list/table ancestors",
  "172": "Engine source at 67566764 and corrected PDF regression tests plus native qpdf/Poppler oracle at c81a613d51579625ac3c1e2c88d93eea30ff8b0a cover actual page boundaries, painted report panels, searchable text, CSS cascade and protected failure output. Exact-current-PR-head CI, genuinely independent review, human approval and a release candidate remain separate.",
  "173": "Explicit p/pre/headings and ordinary div/section/article/blockquote report panels support auto/page; a forced break moves the next report panel to a new real PDF page after the current panel's last fragment. Headings outside lists retain their separate limited break-after:avoid keep hint.",
  "174": "Avoid is not accepted for panels and heading avoid is not general keep. No left/right page targeting, column breaks, break-inside, table/list/inline fragmentation, or full CSS Fragmentation. Unsupported input fails with FPDF1201/1202 without changing the original PDF.",
  "175": "auto/page on explicit p/pre/headings and ordinary report panels outside lists/tables; avoid on headings outside lists only",
  "299": "Legacy print stylesheets may use page-break-before:auto/always; always aliases the same actual PDF page boundary and CSS cascade as break-before:page on explicit paragraphs/headings and scoped ordinary report panels.",
  "300": "Only auto/always or supported CSS-wide keywords. No left/right/avoid, list/table/inline or table-cell paragraph applications, break-inside or complete CSS Paged Media. FPDF1201/1202 leave previous PDF untouched.",
  "301": "Bounded legacy syntax alias and real PDF pagination",
  "302": "Explicit p/pre/headings and ordinary div/section/article/blockquote panels outside lists/tables",
  "303": "Legacy page-break-after:auto/always maps to break-after:auto/page and places the next section after a real PDF page boundary; the heading-only legacy avoid value follows the existing scoped keep hint.",
  "304": "Avoid applies only to headings outside lists, not report panels or ordinary paragraphs. No left/right, break-inside, table/list breaks or general Paged Media; FPDF1201/1202 reject unsupported input without modifying previous PDF.",
  "305": "auto/always on explicit paragraphs/headings and ordinary report panels; avoid on headings outside lists only",
  "389": "Auto/page values produce real PDF page boundaries for explicit paragraphs/headings and ordinary div/section/article/blockquote panels; consecutive forced breaks do not create a phantom middle page.",
  "390": "Only documented LTR report blocks; legacy always aliases page. No left/right page choice, column breaks, break-inside or complete CSS Fragmentation; table cells, lists and inline elements have explicit limits.",
  "391": "Explicit p/pre/headings; ordinary div/section/article/blockquote panels outside lists/tables"
};
export const englishAudit = Object.freeze(previousIndex.map((index, position) => {
  if (index >= 0) return previous[index];
  if (position === 728)
    return previous[718]
      .replace('Current total is 76 scoped CSS property names',
               'Current total is 78 scoped CSS property names') +
      ' As of 2026-11-02 the accepted inventory is 78 scoped CSS property names in 418 stable records. Actual PDF section page breaks and legacy auto/always aliases work only on documented ordinary panels and explicit paragraphs/headings; heading-only avoid remains restricted. General Paged Media, inside/left/right and list/table/inline breaks remain unsupported. Source feature, final-head CI, independent review, human approval and customer distribution are separate.';
  const value = additions[position];
  if (!value) throw new Error('Missing English print page boundary translation: ' + position);
  return value;
}));
if (englishAudit.length !== 732 ||
    englishAudit.some(s => typeof s !== 'string' || !s.trim() || /[\u3400-\u9fff]/u.test(s)))
  throw new Error('Invalid English print page boundary catalogue.');
