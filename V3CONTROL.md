# Additional Input for Beatmap Version 3.0.0
## Switch Map Version

In the map editor, press `alt` + `shift` + `.` to show the dialog box for switching version. Once confirmed, map version will be converted and then it will automatically exit editor. Next time you enter the same map and same difficulty, version is already converted.    
**Note 1**: Please manually backup your map before conversion.     
**Note 2**: Only compatible data(including notes, bombs, obstacles, basic events, customData; excluding arc, chains) will be converted, other may get lost. 
## Arcs
When exactly selecting 2 color notes(need direction), press `V` to create a arc between them.   
The arc contains two notes, which indicate its 2 additional points in `Bezier Curves`. Those two notes can be color-inverted(`mouse mid`), tweaked(`alt` + `scroll` for start note, `shift` + `scroll` for end note)   

## Chains
When exactly selecting 2 color notes(first one needs direction), press `C` to convert them into chains.  
Chain can be color-inverted(`mouse mid`), tweaked note count(`alt` + `scroll`), tweaked squish amount(`shift` + `scroll`)  
**Note 1**: Head note of a chain is still a note, but not chain. Therefore modification on chain doesn't go along with head note, and vice versa.  

## Some Feature could be introduced
- Counters
- Updating while playing for arc is activated by setting `UseChunkLoadingWhenPlaying`. I don't know if it is extremely harmful for performance.
- ~~Audio for chains~~(now note is seperated from chain)
- Dragging for arc/slider.

## Bugs
- The note attached to a chain may sometimes rescale back to the original size. That is because I only check attachment when note/chain is spawned. Therefore, when other events(like change color, refresh pool) happen, attachement check won't get triggered, resulting in a wrong scale.
- `Counters` is not precise. I haven't considered the case when a chain doesn't have head note. (So in fact it's using the old logic)
- Chain & arc don't fade when passing the threshold. That is because they are using `Chunkload`(same as obstacles, since you won't experience obstacle fading), not the same logic as note.