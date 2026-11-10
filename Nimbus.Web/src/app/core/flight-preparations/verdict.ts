/** The Go/No-Go badge text and style for the API's verdict ("green", "yellow", "red" or anything else for unknown). One mapping for every screen. */
export function verdictLabel(verdict: string): string {
  switch (verdict) {
    case 'green':
      return 'GO';
    case 'yellow':
      return 'CAUTION';
    case 'red':
      return 'NO-GO';
    default:
      return 'N/A';
  }
}

export function verdictClass(verdict: string): 'go' | 'caution' | 'nogo' | 'unknown' {
  switch (verdict) {
    case 'green':
      return 'go';
    case 'yellow':
      return 'caution';
    case 'red':
      return 'nogo';
    default:
      return 'unknown';
  }
}
