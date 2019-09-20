export default class Skill {
    constructor(
        public readonly name: string,
        public active: boolean) {}

    public get link(): string {
        return `#${this.name}`;
    }
}