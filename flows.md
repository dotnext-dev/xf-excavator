# Flows

> Developer writes these in plain English. Claude converts to flow runner JSON using `screens.json`.
> Lines prefixed `sim:` become `prompt` actions. Lines prefixed `app:` (or no prefix) become spy actions.

## login happy path
log in with valid user, verify dashboard loads with flights

## login bad credentials
log in with invalid user, verify error shows, verify still on login screen

## login empty submit
try to click login without filling anything, verify button isn't interactive

<!-- Add your flows below. One section per flow. Keep it casual — Claude figures out the steps. -->
