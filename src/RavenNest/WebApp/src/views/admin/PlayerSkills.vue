<template>
<div class="modal-background" @click="close" v-show="visible">
  <div class="modal-statistics" @click.stop :key="revision">
      <h1>Skills for {{playerName}}</h1>   

    <div class="rows" :key="revision">
      <div v-for="stat in getAllStats()" class="row" :key="stat.name">
          <div class="label">{{stat.name}}</div>
          <div class="value">{{stat.level}}</div>
          <div class="value">

              <span v-if="isEditingExp(stat)">
                <input class="table-edit" v-model="stat.experience"/>
                <button class="link-button" @click="applyEditExp(stat)"><i class="fas fa-check"></i></button>
                <button class="link-button" @click="cancelEditExp(stat)"><i class="fas fa-times"></i></button>
              </span>

              <span v-if="!isEditingExp(stat)">
                {{stat.experience}}
                <button class="link-button" @click="editExp(stat)" ><i class="fas fa-pencil-alt"></i></button>
              </span>
          </div>             
      </div>
    </div>
              
  </div>
 </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
    Prop
  } from 'vue-property-decorator';
  import router from 'vue-router';
import { Player, Statistics, Skills } from '@/logic/models';
import GameMath from '@/logic/game-math';
import AdminService from '@/logic/admin-service';

  @Component({
    name: 'PlayerSkills',
    components: { },
  })
  export default class PlayerSkills extends Vue {

    @Prop(Player) player! : Player;
    @Prop(Boolean) visible! : boolean;

    private revision: number = 0;
    private isVisible: boolean = false;
    private skillEdit: Map<string, ExperienceEdit> = new Map<string, ExperienceEdit>();

    public getAllStats(): Skill[] {
        const stats : Skill[] = [];
        let index = 1;
        for(const stat in this.skills) {
          if (stat === 'id' || stat === 'revision') continue;
          const statItem = new Skill();
          statItem.index = index++;
          statItem.name = this.getDisplayName(stat);
          statItem.experience = (this.skills as any)[stat];
          statItem.level = GameMath.expTolevel(statItem.experience);
          stats.push(statItem);
        }
        return stats;
    }

    public isEditingExp(stat: Skill): boolean {
      const edit = this.skillEdit.get(stat.name);
      if (!edit) return false;
      return edit.isEditing;
    }

    public cancelEditExp(stat: Skill) {
      const edit = this.skillEdit.get(stat.name);
      if (!edit) return;
      edit.isEditing = false;
      ++this.revision;
    }

    public applyEditExp(stat: Skill) {
      const edit = this.skillEdit.get(stat.name);
      if (!edit||!this.skills) return;

      AdminService.updatePlayerStat(this.player.userId, stat.name, stat.experience).then(res => {
        if (!edit||!this.skills) return;
        if (res) {
          edit.experience = stat.experience;
          edit.isEditing = false;
          (this.skills as any)[stat.name] = stat.experience;
          ++this.revision;
        }
      });
    }

    public editExp(stat: Skill) {
      console.log('editExp: ' + stat.name);
      let edit = this.skillEdit.get(stat.name);
      if (!edit) {
        edit = new ExperienceEdit();
        edit.index = stat.index;
        edit.name = stat.name;
        edit.experience = stat.experience;
        this.skillEdit.set(stat.name, edit);
      }
      edit.isEditing = true;
      ++this.revision;
    }

    public get skills(): Skills | null {
      if (!this.player) return null;
      return this.player.skills;
    }

    public getStat(name:string):number {
      const stats = this.skills;
      if (!stats) return 0;
      return (stats as any)[name];
    }

    public close() {
      this.visible = false;
      this.$emit('closed');
    }

    public get playerName(): string {
      if (!this.player) return '';
      return this.player.name;
    }

    private getDisplayName(name:string): string {
      let output = name.charAt(0).toUpperCase();
      for(let i = 1; i < name.length; ++i) {
        const letter = name.charAt(i);
        if (letter.toUpperCase() === letter) {
          output += ' ';
        }
        output += letter;
      }
      return output;
    }

  }

  class ExperienceEdit {
    public name: string = '';
    public index: number = 0;
    public experience: number = 0;
    public isEditing: boolean = false;
  }

  class Skill {
    public index: number = 0;
    public name: string = '';
    public experience: number = 0;
    public level: number = 0;
  }

</script>

<style scoped lang="scss">  

input.table-edit {
    padding: 3px;
    margin: 2px;
    width: 150px;
}

button.link-button {
    background-color: #0e0e0f;
    color: #fff;
    border: 0;
    padding: 5px 10px;
    cursor: pointer;
    margin: 2px;
}


  h1 {
    margin-bottom: 30px;
    margin-top: 35px;
  }
  .modal-background {
    position: fixed;
    left: 0;
    top:0;
    right:0;
    bottom:0;
    background-color: rgba(0, 0, 0, .5);
    transition: opacity .3s ease;
  }

  .modal-statistics {
    position: fixed;
    max-width: 100%;
    max-height: 100%;
    background-color: #fff;
    width: 640px;
    left: 50%;
    top: 50%;
    -webkit-transform: translate(-50%,-50%);
    transform: translate(-50%,-50%);
    padding: 10px 25px 25px 25px;
    transition: all .3s ease;
  }

.label {
    min-width: 230px;
    text-align: left;
}

.value {
    font-weight: 500;
    margin-left: 10px;
    text-align: left;
    min-width: 130px;
}

.row {
    display: flex;
    flex-flow: row;
    justify-content: space-between;
    padding: 5px;
    width:100%;
}

.rows {
    display: flex;
    flex-flow: row;
    flex-wrap: wrap;
    justify-content: space-around;
}
</style>