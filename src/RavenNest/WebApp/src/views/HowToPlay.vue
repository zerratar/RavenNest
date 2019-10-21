<template>
  <div class="how-to-play">
    <div class="htp-narrow">
      <h1>How to play</h1>
      <h4>NOTE: This page is still being updated and content may change at any time. Please come back every now and then to make sure you're up to date!</h4>
      <h3>To play the game, you can use the chat commands down below in the Twitch chat. These commands may differ from streamer to streamer. So make sure you read their Twitch panels!</h3>
    </div>

      <div class="command-groups">
       
        <div class="command-group">
          <h2 class="group-name">General</h2>
          <h3>NOTE: Your character is being stored and persisted between streamer and their streams. You can join in with your character anywhere Ravenfall is being played.</h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!join</td>
              <td class="command-description">Join and play the game. This is required before you can do anything in the game. If it is the first time you play the game, character appearance will be randomly generated.</td>
            </tr>
            
            <tr class="command">
              <td class="command-name" valign="top">!leave</td>
              <td class="command-description">Leave the game. Since you can only gain experience in one of the streams you're playing in. It is always recommended that you leave a game before joining a new one.</td>
            </tr>

          </table>
        </div>

        <div class="command-group">
          <h2 class="group-name">Follow your progression</h2>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!stats</td>
              <td class="command-description">Check the current stats of your character. This will print out all your current progress and level of your skills.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!stats &lt;skill name&gt;</td>
              <td class="command-description">Gets the stats of the individual skill by its skill name.</td>
            </tr>            
            <tr class="command">
              <td class="command-name" valign="top">!res</td>
              <td class="command-description">Gets the amount of resources your character currently has.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!island</td>
              <td class="command-description">Tells you where your player currently at.</td>
            </tr>            
          </table>
        </div>

        <div class="command-group">
          <h2 class="group-name">Changing appearances</h2>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!toggle pet</td>
              <td class="command-description">Toggle between your available pets.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!toggle helmet</td>
              <td class="command-description">Toggle the visibility of your helmet; if you have one. This does not affect the stats of the player and is only cosmetic.</td>
            </tr>            
          </table>
        </div>


        <div class="command-group">
          <h2 class="group-name">Training your combat skills</h2>
          <h3>Training Combat skills will increase your characters combat level.<br/><br/>
          It is important train your attack and defense skill if you wish to equip better weapons and armors. Also, health is automatically trained whenever you train a combat skill and cannot be trained by itself.</h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!train all</td>
              <td class="command-description">Trains attack, defense and strength all at once. This will yield 1/3 of the exp over all three skills.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!train atk</td>
              <td class="command-description">Trains attack, attack is required for equipping better weapons. It also increases the chance to hit your maximum damage it is also used to counter the defense of your opponent.</td>
            </tr>            
            <tr class="command">
              <td class="command-name" valign="top">!train def</td>
              <td class="command-description">Trains defense, defense is required for equipping better armors. It also decreases the chance to get hit by your opponents maximum damage.</td>
            </tr>                        
            <tr class="command">
              <td class="command-name" valign="top">!train str</td>
              <td class="command-description">Trains strength, strength is used to calculate the maximum possible melee damage your character can do.</td>
            </tr> 
            <tr class="command">
              <td class="command-name" valign="top">!train magic</td>
              <td class="command-description">Trains magic, magic is an offensive skill that increases damage over level. It does not require attack to improve your chance to hit.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!train ranged</td>
              <td class="command-description">Trains ranged, ranged is an offensive skill that increases damage over level. It does not require attack to improve your chance to hit.</td>
            </tr>
          </table>
        </div>

        <div class="command-group">
          <h2 class="group-name">Training your gathering skills</h2>
          <h3>Increasing your gathering skills will increase your resource yield when participating in the fight against raid bosses. The skills acts as a multiplier so the higher level, the more resources you get!
            The higher level will also increase the amount yielded while training that skill.
          </h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!train mining</td>
              <td class="command-description">Trains mining that yields ore resources for crafting.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!train woodcutting</td>
              <td class="command-description">Trains woodcutting that yields wood resources for crafting.</td>
            </tr>            
            <tr class="command">
              <td class="command-name" valign="top">!train fishing</td>
              <td class="command-description">Trains fishing that yields fish resources for cooking.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!train farming</td>
              <td class="command-description">Trains farming that yields wheat resources for cooking.</td>
            </tr>            
          </table>
        </div>

        <div class="command-group">
          <h2 class="group-name">Crafting equipment</h2>
          <h3>This section is still being updated. <a target="_blank" href="https://github.com/rainstack/Ravenfall-Docs/blob/master/README.md">Please refer to this temporarily crafting guide while we update the website to include the information.</a></h3>

          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!train crafting</td>
              <td class="command-description">Trains your crafting skill. This is required before you can craft anything. This will also consume wood and ore resources over time.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!craft &lt;item type&gt;</td>
              <td class="command-description">Crafts an item of the specific item type specified. You can craft <strong>weapon</strong>, <strong>armor</strong>, <strong>ring</strong>,<strong>amulet</strong></td>
            </tr>            
            <tr class="command">
              <td class="command-name" valign="top">!craft armor</td>
              <td class="command-description">This will craft the best armor piece you can equip. Ex: if you don't have an helmet, it will craft an helmet. If your current helmet is not as good as the one you can craft. It will still craft a helmet.</td>
            </tr>
            <tr class="command">
              <td class="command-name" valign="top">!craft weapon</td>
              <td class="command-description">Crafts a weapon.</td>
            </tr>     
            <tr class="command">
              <td class="command-name" valign="top">!craft ring</td>
              <td class="command-description">Crafts a ring that boosts your character combat stats.</td>
            </tr>            
            <tr class="command">
              <td class="command-name" valign="top">!craft amulet</td>
              <td class="command-description">Crafts a ring that boosts your character combat stats.</td>
            </tr>                        
          </table>
        </div>

     <div class="command-group">
          <h2 class="group-name">Cooking</h2>
          <h3>In the future, you will be able to cook consumable items. This is not implemented yet. But for now you can still train your cooking skill.</h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!train cooking</td>
              <td class="command-description">Trains your cooking skill and also consumes wheat and fish while training.</td>
            </tr>                                        
          </table>
      </div>

     <div class="command-group">
      <h2 class="group-name">Sailing</h2>
      <h3>You can train sailing or just sail between islands. Keep in mind that if the captain seat is free. You will grab it!</h3>
      <table class="commands">
        <tr class="command">
          <td class="command-name" valign="top">!sail away</td>
          <td class="command-description">Sails to the pirate island, this island requires combat level 75 for training combat skills and at least 90 woodcutting to train woodcutting.</td>
        </tr>
        <tr class="command">
          <td class="command-name" valign="top">!sail home</td>
          <td class="command-description">Sails back to the home island, this can be used after !sail if you want to make sure you disembark at home.</td>
        </tr>
        
        <tr class="command">
          <td class="command-name" valign="top">!sail</td>
          <td class="command-description">Sail without a destination. You will keep sailing the seven seas until you feel contempt and disembark.</td>
        </tr>

        <tr class="command">
          <td class="command-name" valign="top">!disembark</td>
          <td class="command-description">Feeling seasick? Disembark at the closest island. This will stop your character from sailing as soon as they reach a dock.</td>
        </tr>        
      </table>
    </div>

     <div class="command-group">
          <h2 class="group-name">Fighting other players</h2>
          <h3>In the future, there will be an open pvp area that you can join to fight players for rewards. Right now, you will have to settle with duels and arena. Hmpf</h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!duel &lt;player name&gt;</td>
              <td class="command-description">Sends a duel request to a target player for a 1-on-1 fight. Great if you want to relieve some stress against someone else. If you already have an active request from that player, you can accept a duel with this command as well.</td>
            </tr>

            <tr class="command">
              <td class="command-name" valign="top">!duel accept</td>
              <td class="command-description">Accepts a pending duel request.</td>
            </tr>            

            <tr class="command">
              <td class="command-name" valign="top">!duel decline</td>
              <td class="command-description">Declines/rejects a pending duel request.</td>
            </tr> 

            <tr class="command">
              <td class="command-name" valign="top">!arena</td>
              <td class="command-description">Join in the arena to fight a free-for-all type of PvP. Last person standing wins! You can only join the arena while on the home island.</td>
            </tr> 

          </table>
      </div>

      
     <div class="command-group">
          <h2 class="group-name">Raid bosses</h2>
          <h3>Raid bosses spawns every 15-50mins after eachother. A raid boss can also be force started by a moderator or the broadcaster.</h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!raid</td>
              <td class="command-description">Joins the active raid and teleports the player to the appropriate island. When the raid is over, the player is teleported back.</td>
            </tr>

            <tr class="command">
              <td class="command-name" valign="top">!raid start</td>
              <td class="command-description">Broadcaster and Mods only: This will spawn a raid boss for the players to fight.</td>
            </tr>                    
          </table>
      </div>

    
     <div class="command-group">
          <h2 class="group-name">Raiding streamers</h2>
          <h3>Broadcasters Only: Whenever you're done streaming, as usual. You should raid someone!</h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!raid &lt;streamer name&gt;</td>
              <td class="command-description">Bring all your players into the target streamers instance of Ravenfall without having them use the !join command. These players will have your logo/profile picture above their names ingame.</td>
            </tr>

            <tr class="command">
              <td class="command-name" valign="top">!raidwar &lt;streamer name&gt;</td>
              <td class="command-description">Much like !raid, this will bring all players but it will declare war against the target streamer and both sides will clash and fight eachother. A great fun event for whenever you raid a streamer with equal amount of players.</td>
            </tr>                    
          </table>
      </div>

     <div class="command-group">
          <h2 class="group-name">Accessing the marketplace</h2>
          <h3>The marketplace is global and persisted over time, multiple streamers can have players buying and selling items to the same marketplace. You can buy items being in one stream that is sold from a player from another stream.
            <br/>This section is still being updated. Please use the temporarily <strong>!market</strong> command by major_lift in zerratar stream to see marketplace listings. <a target="_blank" href="https://gist.github.com/rainstack/1377ec1beba638bfc76699ac398f40ad">See example</a>
          </h3>
          <table class="commands">
            <tr class="command">
              <td class="command-name" valign="top">!buy &lt;item name&gt; &lt;amount&gt; &lt;max buy price per item&gt;</td>
              <td class="command-description">Buys the amount of items from the marketplace matching the item name and amount provided. You will always buy the cheapest items on the marketplace, the buy amount can be specified by using: <blockquote>x10</blockquote> where the <i>x</i> is required, but if left out x1 is used per default. The max price can be defined by using postfix like: <blockquote>10k</blockquote>, available postfixes: <strong>k</strong>,<strong>m</strong>,<strong>b</strong></td>
            </tr>

            <tr class="command">
              <td class="command-name" valign="top">!sell &lt;item name&gt; &lt;amount&gt; &lt;sell price per item&gt;</td>
              <td class="command-description">Puts an item on the marketplace matching the item name provided, the amount can be specified by using: <blockquote>x10</blockquote> where the <i>x</i> is required. The max price can be defined by using postfix like: <blockquote>10k</blockquote>, available postfixes: <strong>k</strong>,<strong>m</strong>,<strong>b</strong></td>
            </tr>                    
          </table>
      </div>

<!-- https://github.com/rainstack/Ravenfall-Docs/blob/master/README.md -->

      </div>

  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue
  } from 'vue-property-decorator';
  import router from 'vue-router';

  @Component({
    components: { },
  })
  export default class HowToPlay extends Vue {

  }
</script>

<style scoped lang="scss">

.htp-narrow {
  width: 580px;
  max-width: 100%;
  margin-left: auto;
  margin-right: auto;
}

.how-to-play {
    width: 880px;
    margin-left: auto;
    margin-right: auto;
    max-width: 100%;  
    margin-top: 112px;
}
.command-description blockquote {
    display: inline-block;
    margin-top: 0;
    margin-bottom: 0;
    margin: 0;
    background-color: #f1f1f1;
    padding: 0 5px;
}
.command-group h3 {
    text-align: left;
    margin-bottom: 30px;
    font-size: 12pt;
    background-color: #fbfbfb;
    padding: 20px;
    font-style: italic;
    margin-left: auto;
    margin-right: auto;    
}

h2.group-name {
    margin-bottom: 10px;
}
.command-group {
  display: flex;
  flex-flow: column;
  align-items: flex-start;
  border-bottom: 1px solid #e1e1e1;
  padding-bottom: 25px;
}
.command-groups {
    width: 880px;
    max-width: 100%;
    margin-left: auto;
    margin-right: auto;
}
td.command-description {
    text-align: left;
}
table.commands{
  width: 100%;
}
.command-name {
    font-weight: bold;
    padding-right: 10px;
    text-align: right;
    white-space: nowrap;
}

</style>