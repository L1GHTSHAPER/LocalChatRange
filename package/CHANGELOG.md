# Changelog

## 1.1.1

- Cream panels, warm brown text, coral accents, rounded controls and game fonts.
- Grouped square side buttons with brown outlines, proportionate icons and name/hotkey hints. Buttons avoid open panels and hide with game controls in Desktop mode.
- Responsive settings menus, tabs and collapsible advanced options. Existing configuration keys, commands and English/Russian support are preserved.
- Input guards keep menu editing and scrolling from moving the player or camera.
- Updated menu instructions and direct support links for this mod on Thunderstore and GitHub.
- Settings tabs: General, Range, Appearance and Quality. Color previews show alpha; invalid drafts retain saved values. F8 and /chatrange retain their actions.

## 1.0.3

- Updated the package icon and README with the rounded LightShaper logo and black, white and purple visual style.
- Plugin behavior is unchanged.

## 1.0.2

- Added the public GitHub repository link to the package website and README.
- Published source code and packaged downloads on GitHub. Mod behavior is unchanged.

## 1.0.1

- Fixed: the show/hide hotkey (F8) did nothing while another key was held, e.g. W while walking.

## 1.0.0

- Initial release.
- Local chat range drawn on the surface around your character; follows slopes, stairs and drops.
- Rings under players who will receive your local messages.
- F8 hotkey, `/chatrange` (`/lcr`) chat command, three visibility modes.
- The radius is read from the game code at startup, with a config fallback.
- Optional integration with CommandAPI (`/help` listing, CommandTypeahead suggestions).
