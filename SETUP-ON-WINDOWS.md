# Move this branch to GridFinder (Windows PowerShell)

You pulled this from CopperHead only as a temporary handoff.
Run these commands exactly (copy/paste one block at a time).

```powershell
cd C:\Users\today
git clone --branch cursor/gridfinder-handoff-4242 --single-branch https://github.com/uberslaw/CopperHead.git gf-src
cd gf-src
git remote set-url origin https://github.com/uberslaw/GridFinder.git
git push -u origin HEAD:main
```

Then open https://github.com/uberslaw/GridFinder — the code should be there.

To run it later:
```powershell
cd C:\Users\today\gf-src
npm install
npm start
```
