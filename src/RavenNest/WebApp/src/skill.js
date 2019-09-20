export default class Skill {
    constructor(name, active) {
        this.name = name;
        this.active = active;
    }
    get link() {
        return `#${this.name}`;
    }
}
//# sourceMappingURL=skill.js.map