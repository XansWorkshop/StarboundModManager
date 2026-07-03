# Wishlist

This is a collection of things I'd like to do in the future.

***

### Features To Add / Known Problems

1. Add the ability to import collections, rather than just individual mods your current subscriptions.
    * n.b. Steam currently has no way to install complete collections. If I can figure out a workaround, I'll get it added, but for now I don't think there's a way other than subscribing to the collection, running and closing the game so Steam installs the mods, and then importing your subscriptions. Sorry.
2. Automated setup.
    * This relies on some internal stuff, see below.

### Internal Code

1. Fix the spaghetti, there's lots of it.
    1. For general tasks, try to come up with some uniform way to start a task and include the ProgressWindow in on it.
    2. To self: You solved a lot of these problems in The Conservatory, but the code is deeply embedded and not portable at all.
2. Make more of the menu asynchronous for responsiveness.
3. Add a right click menu to the main page alongside using the topbar buttons.
4. **Automated setup:** Consider if automating SteamCMD is possible.
    * There is basically no reason to NOT have SteamCMD, since it limits the main benefit of the app, workshop downloading.
    * See if Steam's licensing allows distributing it directly? This would make auto-installation available for Mac and Linux, which currently require using the command line, which I'd like to avoid doing on the user's behalf. Maybe. That concern could be useless.
    * If SteamCMD is required, the game can be installed automatically too.
    * Figure out how to automate the OpenStarbound installation. GitHub doesn't like it when machines scrape user-facing webpages because it bypasses their API.