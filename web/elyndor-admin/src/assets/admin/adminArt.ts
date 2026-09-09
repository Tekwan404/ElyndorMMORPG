const adminArtModules = import.meta.glob<string>('./*.{png,jpg,jpeg,webp}', {
  eager: true,
  import: 'default',
})

export const adminArtCatalog = Object.fromEntries(
  Object.entries(adminArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    return [fileName.replace(/\.[^.]+$/, ''), url]
  }),
)

export function adminArtUrl(assetId: string): string | undefined {
  return adminArtCatalog[assetId]
}
