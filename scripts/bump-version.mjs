#!/usr/bin/env node
import { readFileSync, writeFileSync } from 'fs';
import { execSync } from 'child_process';

const newVersion = process.argv[2];
if (!newVersion || !/^\d+\.\d+\.\d+$/.test(newVersion)) {
  console.error('Usage: node scripts/bump-version.mjs <major.minor.patch>');
  process.exit(1);
}

// Bump package.json
const pkg = JSON.parse(readFileSync('web/package.json', 'utf8'));
const oldVersion = pkg.version;
pkg.version = newVersion;
writeFileSync('web/package.json', JSON.stringify(pkg, null, 2) + '\n');
console.log(`web/package.json  ${oldVersion} → ${newVersion}`);

// Bump .csproj
const csprojPath = 'api/OsrsTracker.Api/OsrsTracker.Api.csproj';
const csproj = readFileSync(csprojPath, 'utf8');
const updated = csproj.replace(/<Version>.*?<\/Version>/, `<Version>${newVersion}</Version>`);
writeFileSync(csprojPath, updated);
console.log(`OsrsTracker.Api.csproj  ${oldVersion} → ${newVersion}`);

// Commit and tag
execSync(`git add web/package.json ${csprojPath}`);
execSync(`git commit -m "chore: bump version to ${newVersion}"`);
execSync(`git tag v${newVersion}`);

console.log(`
✓ Committed and tagged v${newVersion}

Next steps:
  git push origin HEAD        push the commit (then open a PR)
  git push origin v${newVersion}   push the tag after merging
`);
