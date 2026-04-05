import { execSync } from "child_process";
import type { InstalledGame } from "../types";

interface GogGame {
  id: string;
  name: string;
  installPath: string;
}

function runPowerShell(script: string): string {
  const encoded = Buffer.from(script, "utf16le").toString("base64");
  return execSync(`powershell -NoProfile -NonInteractive -EncodedCommand ${encoded}`, {
    encoding: "utf-8",
    timeout: 10000,
    stdio: ["ignore", "pipe", "ignore"],
  });
}

function getGogGamesWindows(): GogGame[] {
  const script = `
$results = @()
$keys = @('HKLM:\\SOFTWARE\\WOW6432Node\\GOG.com\\Games', 'HKLM:\\SOFTWARE\\GOG.com\\Games')
foreach ($key in $keys) {
  if (-not (Test-Path $key)) { continue }
  Get-ChildItem $key | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if ($p -and $p.gameName -and $p.path) {
      $id = if ($p.gameId) { $p.gameId } elseif ($p.productId) { $p.productId } else { '' }
      $results += [PSCustomObject]@{ id = $id; name = $p.gameName; path = $p.path }
    }
  }
}
if ($results.Count -eq 0) { Write-Output '[]'; return }
$results | ConvertTo-Json -Compress -Depth 1
`;

  try {
    const output = runPowerShell(script).trim();
    if (!output || output === "[]") return [];
    const parsed: unknown = JSON.parse(output);
    const items = Array.isArray(parsed) ? parsed : [parsed];
    return items
      .filter((item): item is Record<string, string> => !!item && typeof item === "object")
      .filter((item) => item.name && item.path)
      .map((item) => ({ id: String(item.id ?? ""), name: String(item.name), installPath: String(item.path) }));
  } catch {
    return [];
  }
}

export async function detectGogGames(): Promise<InstalledGame[]> {
  if (process.platform !== "win32") {
    // macOS/Linux: GOG Galaxy database detection not implemented
    return [];
  }

  return getGogGamesWindows().map((g) => ({
    name: g.name,
    store: "gog" as const,
    storeGameId: g.id || undefined,
    installPath: g.installPath,
  }));
}
