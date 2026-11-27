using System;
using System.Collections.Generic;

namespace RavenNest.Blazor.Services.Announcements
{
    /// <summary>
    ///     News that ships with the server: a post written ahead of a release, put on the site by itself once the game
    ///     version it is about is out (<see cref="AnnouncementService.SeedReleaseNews"/>).
    /// </summary>
    /// <remarks>
    ///     A post goes in once, the first time the server sees the game at <see cref="ReleaseNewsPost.Version"/> or
    ///     newer (the release watcher updates that from GitHub), so a server deployed ahead of the release does not show
    ///     it early. After that it is an ordinary post: it can be edited, unpinned or deleted in the admin panel, and a
    ///     deleted one does not come back (the ids that went in are kept beside the posts). Discord is still sent by
    ///     hand from the admin panel, as for every post.
    /// </remarks>
    public static class ReleaseNews
    {
        public static readonly IReadOnlyList<ReleaseNewsPost> Posts = new[]
        {
            new ReleaseNewsPost
            {
                Id = new Guid("3f2b9c1e-7a4d-4e58-9b0c-097a0f1d2e01"),
                Version = new Version(0, 9, 7),
                Pinned = true,
                Post = new Announcement
                {
                    Title = "Ravenfall 0.9.7: the new combat, the tower, battle pets and gear for your role",
                    Summary = "Fights now come down to roles, elements and the skills you choose. Streams can climb a tower together, floor by floor, battle pets fight at your side, and your gear and outfit follow your role. Here is what changes for you and how to start.",
                    Author = "Zerratar",
                    Kind = AnnouncementKind.Update,
                    Body = @"Your character keeps everything it has: levels, items, coins, all of it. What changes is how a fight plays out, and three ideas cover most of it.

**Roles.** What you train and what you wear decide your part in a fight. Defense and heavy armor hold the enemy's attention, Healing keeps the others standing, and everyone else deals the damage. Enemies hit whoever worries them most, so a tank in front matters, most of all against bosses.

**Elements.** Many enemies now strike with fire, ice, lightning or poison, and each has a weakness. Gear can carry element damage and resistances, and gems in your gear's sockets add more. Bring fire against ice and you hit harder; resist what an enemy throws and you take less.

**Skills.** Your levels give you skill points to spend in skill trees. Passives always count, and the active skills in your loadout are used by your character on its own in fights. Each tree ends in three capstones and you pick one. Gatherers and crafters have trees of their own (Gathering and Artisan), and so do tamers (Beastmaster). There is nothing to type during a fight: plan a build on the website or in chat, and your character fights with it.

## Health is now Vitality

The Health skill is called Vitality now (`!train vitality`, and `!train health` still works). Your health is 8 per Vitality level plus what your armor adds, so the numbers look different from before. Nothing was taken away.

## The tower

A climb for the whole stream: `!tower` takes you in. The tower stands in the sea south of Home, and you can sail there with `!sail tower` to see your stream's best climbers carved in stone. It has floors, each a world of its own with a town, and every floor has parts. A part has objectives that count by themselves while the stream plays (its monsters defeated, materials gathered wherever you train, hidden things found) and then a guardian: a mini boss, or the floor boss on a floor's last part, which opens the next floor. The first {top} floors are open, and more come with later updates.

Every floor asks for higher levels than the one below, and pays more for the climb: 5% more experience for every floor, never less than your island would pay, and Slayer experience from every guardian you help defeat. The fights follow the group: more players, or stronger ones, meet tougher monsters and guardians, and a small stream meets smaller ones.

Every floor's town has people to talk to, quests of your own and a campfire to rest at. The streamer or a moderator decides when to fight the guardian and when to climb on, or the players in the tower vote: `!tower boss`, `!tower climb`, `!tower close`. A part your stream has cleared stays open: `!tower part 3` takes you back there to train while the climb goes on. The tower panel has a map of the floor (`!tower map`). `!tower leave` takes only you out, any time.

## Gear for your role

Thirteen lines of class gear, from Mage robes and Healer vestments to Tank plate, Rogue leathers and Beastmaster furs, crafted from Linen (woven from Flax, a new crop) and Leather (tanned from Hide you find while fighting). `!equip tank`, `!equip mage` or `!equip healer` dresses you for a role, and your outfit follows what you train: robes for Magic, plate for Defense, whatever you like for Fishing. `!outfit` shows and pins it.

## Rare finds

The hardest bosses hide rare things: Dark Fortress weapons, armor looks and helmets, and for the strongest players five Legendary weapons, each with a power of its own, in the [Hall of Legends](/legends). And now and then a piece of gear with **Substitution**, or a **Shadowstone** gem: when a boss's slam or spit lands, you might vanish in a puff of smoke or slip out as a shadow and take no damage at all.

## Battle pets

Taming is a new skill. Pick a first pet with `!pets starter fox` (damage), `bear` (tank) or `fawn` (healer), tame creatures you fight with `!tame bunny`, and hatch eggs from quests, raids, dungeons and the tower with `!pets hatch`. A pet that is out fights beside you, levels up and evolves. `!pets` shows yours.

## Your character page

The Equipment tab shows your character in 3D in what it wears, with a card that compares every piece and your outfits per skill. The Inventory tab is grouped by kind, with big or small tiles, or everything at once.

## Commands to try

- `!tree` shows your skill points and loadout, `!learn cleave` spends them, `!respec` gives them back
- `!gems` lists your gems and what they do, `!socket rune sword ruby` puts one in
- `!equip tank` and `!outfit` for your role's gear
- `!pets` and `!tame` for battle pets
- `!tower` for the tower, `!sail tower` to visit it

## Read more

- [How combat works](/combat)
- [Skill trees and the build planner](/skill-trees)
- [The tower](/tower)
- [Battle pets](/pets)
- [Hall of Legends](/legends)
"
                }
            },
        };
    }

    public sealed class ReleaseNewsPost
    {
        /// <summary>Fixed, so the post goes in once whatever the title becomes.</summary>
        public Guid Id { get; set; }

        /// <summary>The game version it announces: it goes on the site once the server sees this version or newer.</summary>
        /// <remarks>In the body, {top} becomes the number of tower floors there are when it goes in.</remarks>
        public Version Version { get; set; }

        public bool Pinned { get; set; }

        public Announcement Post { get; set; }
    }
}
