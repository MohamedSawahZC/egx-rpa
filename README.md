# EGX Client Coding — RPA Proof of Concept

**Read `RUNBOOK.md` first.** It explains what each folder is, how to run the
demo, and what has and has not been verified.

## Quick demo, in order

1. `2-SheetSimulation\RUN-GenerateSheet.cmd`  — make the KYC sheet
2. `1-DesktopAppSimulation\RUN-App.cmd`       — open the fake EGX app, leave it open
3. `3-RPA-Bot\RUN-Discover.cmd`               — discover the fields by their Arabic labels
4. `3-RPA-Bot\RUN-1-Validate.cmd`             — local validation gate
5. `Ctrl+F8` in the app                       — simulate EGX replying

## Nothing here touches EGX

The simulator is a stand-alone fake. It is not connected to the exchange, sends
nothing anywhere, and every code it issues is invented. All client data is
synthetic.