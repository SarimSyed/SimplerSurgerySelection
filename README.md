# Simpler Surgery Shortcut (will probably rename it)

One of the things I found really annoying was when one of the pawns were missing a limb or organ, the default dropdown for surgeries would be big enough to cover the HeDiffs and I'd close it to make sure if it's the left or right that I need to replace. I don't have a small screen so I'm sure others with smaller screens might also have a similar experience.

[Dubs Mint Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594) was a really big improvement sine not only is it nicer to look at but with it being to the right of the injuries menu the side of the injury was always visible. But there was still the issue of having to scroll through to find the correct surgery. The search function didn't work for me, but even if it did I'm lazy. I should be able to click on the injury and see the relevant surgeries.

So thats what I made. Just click on an injury and you'll see a dropdown with surgeries you can do on that part. Right now both sides are displayed when you click either the left or right body part and that's what I plan on fixing next.

## Requirements

You need Harmony for this mod since access to some private fields and methods was needed. Load order shouldn't matter as long as its after Harmony and the Core files.

## Future Plans

This was my first "complex" mod and was the perfect way to brush up on my C# skills while i'm on break from shool, so i plan on maintaining it until i feel like its perfect. What will make it perfect?

~~- Whether an injury on the left or right is clicked the surgery options for both side even if the other side is perfectly healthy is there. I'd like only the correct side to be displayed.~~
~~- The surgery options are not being filtered properly so at the moment organs don't have the surgeries options popping up when you click injuries on them. This is because of how i'm filtering the list of recipes.~~

- Not sure if it works properly with the Multiplayer Mod but if it doesn't I'd like to fix that

Works but needs more testing
