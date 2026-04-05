import { detectSteamGames } from "./detectors/steam";
import { detectGogGames } from "./detectors/gog";
import { detectEpicGames } from "./detectors/epic";
import { detectXboxGames } from "./detectors/xbox";
import type { InstalledGame } from "./types";

export async function detectInstalledGames(): Promise<InstalledGame[]> {
  const results = await Promise.allSettled([
    detectSteamGames(),
    detectGogGames(),
    detectEpicGames(),
    detectXboxGames(),
  ]);

  const games: InstalledGame[] = [];
  for (const result of results) {
    if (result.status === "fulfilled") {
      games.push(...result.value);
    }
    // Silently ignore stores that failed to detect
  }

  return games;
}
