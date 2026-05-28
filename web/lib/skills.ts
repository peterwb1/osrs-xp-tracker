const OVERRIDES: Record<string, string> = {
  Overall: 'https://oldschool.runescape.wiki/images/Stats_icon.png',
  Runecraft: 'https://oldschool.runescape.wiki/images/Runecrafting_icon.png',
};

export function skillIconUrl(name: string): string {
  return OVERRIDES[name]
    ?? `https://oldschool.runescape.wiki/images/${name}_icon.png`;
}
