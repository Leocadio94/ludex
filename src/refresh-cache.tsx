import { showToast, Toast } from "@raycast/api";
import { refreshManifest } from "./lib/manifest";

export default async function Command() {
  const toast = await showToast({
    style: Toast.Style.Animated,
    title: "Refreshing game database...",
  });

  try {
    const index = await refreshManifest((msg) => {
      toast.message = msg;
    });

    toast.style = Toast.Style.Success;
    toast.title = "Game database refreshed";
    toast.message = `${Object.keys(index).length.toLocaleString()} games indexed`;
  } catch (err) {
    toast.style = Toast.Style.Failure;
    toast.title = "Failed to refresh";
    toast.message = String(err);
  }
}
