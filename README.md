# Starbound Mod Manager

Starbound Mod Manager (SBMM) is an unofficial, third party tool designed to manage *instances* of Starbound, a bit like a mod manager for other games. Its primary goal is to allow you to have multiple, separate instances of the game, each with their own mods and saves.

## Installation Instructions

Download SBMM at this link, or from the releases page: [Download SBMM]()

It will help you to set itself up with a wizard that guides you step by step. No technical knowledge required.

## Directories and Uninstallation

SBMM stores your instances in `%appdata%/Xan's Workshop/StarboundModManager`. This includes several directories:

1. **Profiles** stores your various instances.
2. **Workshop** stores cached/copied Workshop mods. **This is NOT your Steam Workshop install directory.** The purpose of this folder is outlined in the section below.
3. **Settings.cfg** stores your program settings.

## Workshop Integration

SBMM recognizes, but otherwise bypasses, the Steam Workshop. **You do NOT need to uninstall your Workshop mods to use SBMM.**

When you create a new modpack, you have the option to **Sync Steam Workshop**. If you sync, this happens:

1. SBMM will go through your Steam library directory and find all of your Workshop mods.
2. SBMM will copy every Workshop mod you have installed right now, and store them in a shared cache that every modpack uses.
3. The list of installed Workshop mods is stored as part of your new modpack's profile.
4. When you launch the game, Starbound is told **not** to load from the real Steam Workshop, and is told to instead load mods from the shared cache.

## Sharing Modpacks

> ![NOTE]
> Steam does not offer a way for third party programs (like SBMM) to download from the Steam Workshop. Sharing modpacks requires sharing large archive files. Sorry.

SBMM allows you to import and export modpacks as full archives. These archives contain the entirety of every mod that is installed. You can optionally choose to exclude Workshop mods, but this means whoever receives your pack will have to manually subscribe to those mods, let SBMM grab them, and then unsubscribe.