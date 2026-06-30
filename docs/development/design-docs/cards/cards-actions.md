# Cards — Action Inventory & Card Composition

Extracted verbatim from `config/Monopoly Cards and Types.xlsx`. The raw data behind
`cards-design.md` — the action vocabulary and the real cards, used to pin the
`TriggerEvent`/condition set and (later) `card-decks.md`.

**Model (per the sheet):** each **action** has a `No.`, a **category** (the sheet tab —
Buildings / Card / Change Direction / Downgrade / Immunity / Jail / Loans / Money /
Property / Reservation / Turns / Mortgage / Movement), instruction text, the keep/condition
flags, and an optional **Link** to another action by `No.`. A **card** is one connected group
of Link-joined actions (an unlinked action is a one-action card). Empty rows (a `No.` with no
instructions) are omitted.

**Condition flags** map to `cards-design.md` §5: `Keep` = `IsKeepUntilNeeded`; `Met·Self`/`Met·Other`
= `MetCardholderTurn`/`MetAnyPlayerTurn` (forced); `Choice·Self`/`Choice·Other` = the `Choice*` types.

**Totals:** 206 actions (rows), 170 cards (29 multi-action, 141 single-action). The `Turns` cards **2** ("miss 3 turns"), **7** ("extra 3 turns") and **8 + 9** ("miss 3 turns OR extra 3 turns") were **added by hand** — their rows are blank in the Excel, so they aren't part of the verbatim extract and carry no condition flags yet.

> **⚠ Data note — `No.` is not unique.** Three numbers appear on two sheets each (none are
> Link-joined, so card grouping is unaffected — but confirm whether each is *one* card with
> two actions, or a duplicate/mis-category to fix:
> - **79** — Buildings: "When you get a new set you will receive 1 house for each property in the set."  /  Property: "Swap 1 of your properties with any property of any player - excluding properties with buildings"
> - **113** — Downgrade: "Your double is converted into a triple"  /  Money: "Your double is converted into a triple"
> - **114** — Downgrade: "Dodgy judge facilitates a double in jail to turn into a triple. No triple card to be taken."  /  Jail: "You know a corrupt prison guard. The next time you are in jail you will receive rent"

---

## Cards

### Multi-action cards (Link-joined)

**Card [3 + 4 + 5]**
- **3** _(Loans)_ — All outstanding loans are wiped out
- **4** _(Money)_ — Any player with no loan receives £1,000
- **5** _(Property)_ — Must return a property to the bank

**Card [8 + 9]**
- **8** _(Turns)_ — Miss 3 turns or
- **9** _(Turns)_ — have an extra 3 turns

**Card [16 + 17]**
- **16** _(Card)_ — Pass any retained card to the player rolling the lowest number with 1 dice
- **17** _(Card)_ — 1 card to each player who rolls the lowest number

**Card [20 + 21]**
- **20** _(Buildings)_ — Bad news! Purge 1 of your properties
- **21** _(Property)_ — Also hand back a property to the bank

**Card [37 + 38]**
- **37** _(Money)_ — You are assessed for street repairs, pay £80 for each house
- **38** _(Money)_ — Pay £230 for each hotel

**Card [40 + 41]**
- **40** _(Money)_ — Make general repairs on all of your buildings. Pay £50 per house
- **41** _(Money)_ — Pay £200 for each hotel

**Card [43 + 44]**
- **43** _(Money)_ — Pay each player £100 and
- **44** _(Jail)_ — Go to jail

**Card [45 + 46]**
- **45** _(Money)_ — Pay each player £5,000 - %age applies and
- **46** _(Jail)_ — Go to jail

**Card [51 + 52]**
- **51** _(Money)_ — You have been robbed by an escaping prisoner. Pay £300 to Free Parking and
- **52** _(Property)_ — Return 1 property to the bank

**Card [77 + 78]**
- **77** _(Property)_ — Swap a set with any player and
- **78** _(Buildings)_ — Both sets get purged

**Card [151 + 152]**
- **151** _(Money)_ — You are assessed for street repairs, pay £50 per house
- **152** _(Money)_ — Pay £100 per hotel

**Card [155 + 156]**
- **155** _(Money)_ — Make general repairs on all of your buildings. Pay £50 per house
- **156** _(Money)_ — Pay £100 per hotel

**Card [167 + 168 + 169]**
- **167** _(Movement)_ — Advance to Go!, do not take a card and
- **168** _(Money)_ — Collect £200 and
- **169** _(Money)_ — All other players receive £100 times 1 dice

**Card [170 + 171]**
- **170** _(Money)_ — Recive the bonus and
- **171** _(Movement)_ — Go to jail for 5 turns _(keep)_

**Card [172 + 173]**
- **172** _(Money)_ — Receive the bonus but must hand back have of your existing cash or
- **173** _(Property)_ — Or return a set to the bank

**Card [174 + 175 + 176]**
- **174** _(Money)_ — You must make a choice, no bonus or
- **175** _(Money)_ — receive the bonus plus
- **176** _(Property)_ — Plus return two properties to the bank

**Card [177 + 178]**
- **177** _(Movement)_ — Advance to Go!, do not take a Go! Card and
- **178** _(Money)_ — and roll 2 dice times £100

**Card [179 + 180]**
- **179** _(Money)_ — You steal £200 from each player and the bank and
- **180** _(Movement)_ — and go to jail

**Card [181 + 182]**
- **181** _(Movement)_ — Advance to the nearest station owned by someone else but
- **182** _(Turns)_ — but miss a turn

**Card [183 + 184]**
- **183** _(Movement)_ — Swap places with another in jail. Other players roll to see who it is and
- **184** _(Money)_ — and you also swap the amounts to come out of jail

**Card [188 + 189]**
- **188** _(Movement)_ — Swap places with any other player and
- **189** _(Money)_ — and both players receive £200

**Card [190 + 191 + 192 + 193]**
- **190** _(Money)_ — Roll 2 dice and
- **191** _(Money)_ — and multiply the total by the third dice and
- **192** _(Money)_ — and receive the final total times £200 %age applies and
- **193** _(Property)_ — must return 1 or 2 or properties to the bank

**Card [194 + 195]**
- **194** _(Money)_ — Pay the tax that is due but
- **195** _(Buildings)_ — but receive a free hotel which has to be the next hotel you erect

**Card [197 + 198 + 199]**
- **197** _(Money)_ — Pay double or
- **198** _(Money)_ — Pay half and
- **199** _(Movement)_ — and go to jail

**Card [200 + 201]**
- **200** _(Money)_ — Pay the tax and roll 1 dice and
- **201** _(Property)_ — and if even, receive a property from Free Parking, if none

**Card [202 + 203 + 204]**
- **202** _(Property)_ — Recive a property from the bank, if none
- **203** _(Money)_ — if none, the bank pays you £3,000 and
- **204** _(Property)_ — if odd, put a property into Free Parking. It is not recordded

**Card [207 + 208]**
- **207** _(Money)_ — Collect £500 and
- **208** _(Movement)_ — and do not turn around

**Card [212 + 213]**
- **212** _(Property)_ — Return a set to the bank or
- **213** _(Money)_ — or pay £100 times the number of properties you own

**Card [214 + 215]**
- **214** _(Money)_ — Your lucky day! Proceed as normal but first take £1,500 if
- **215** _(Money)_ — if there is a shortfall it is paid by the bank, other cards can then be played

### Single-action cards

| No. | Category | Keep | Card |
|---|---|---|---|
| 1 | Change Direction | ✓ | Change direction |
| 2 | Turns |  | Miss 3 turns |
| 6 | Change Direction |  | each player changes directions including those in jail |
| 7 | Turns |  | Have an extra 3 turns |
| 10 | Downgrade | ✓ | Your next triple is downgraded to a double |
| 11 | Downgrade | ✓ | Converet a double into a triple |
| 12 | Downgrade | ✓ | Cancel a player's triple bonus |
| 18 | Money |  | Your credit rating plummets! Your outstanding loan has been increased by £3,000 |
| 19 | Buildings |  | A friend in jail arranges for someone to purge 2 properties of your choice |
| 23 | Movement |  | Go back to Bow Street |
| 24 | Movement |  | Go back to income tax |
| 25 | Movement |  | Go back 11 spaces |
| 26 | Movement | ✓ | After next roll, go back 17 spaces |
| 28 | Movement |  | Go back 1 space |
| 30 | Movement |  | Go back to Income Tax and pay £200 |
| 31 | Immunity | ✓ | Immunity from triple bonus being cancelled, play once |
| 32 | Immunity | ✓ | Immunity from swapping all money with another player |
| 33 | Immunity | ✓ | Immunity from any card drawn when landing on go to to jail, play once |
| 34 | Immunity | ✓ | Immunity from any Go To Jail card, play once. |
| 35 | Money |  | You have been robbed. Pay £1,000 to the player rolling the highest with 1 dice. |
| 36 | Money |  | Pay university fees of £1,000 |
| 39 | Money |  | You fail a breathalyser test. Pay £500 |
| 42 | Money |  | Speeding fine. Pay £300 |
| 47 | Money |  | Fine of £1,000 imposed by the judge, %age applies. Do not go to Jail or Just Visiting |
| 48 | Money |  | You make a donation to charity. Pay £100 per property that you own. |
| 49 | Money |  | £400 fine or take a %age Chance |
| 50 | Money |  | Burst tyre, pay £200 |
| 53 | Money |  | The tax payment is tripled |
| 54 | Money |  | The tax payment is halved |
| 55 | Money |  | The tax is to bepaid by the player rolling the lowest number with 1 dice |
| 56 | Money | ✓ | Unlucky! No money for landing on Go! For 5 turns including this turn |
| 57 | Money |  | You receive no money for the Triple |
| 58 | Money | ✓ | You receive no cash on your next visit to Free Parking |
| 59 | Money | ✓ | Your next payment to another player is doubled |
| 62 | Movement |  | #Advance to any space on the board without being able to land on Go! Or Free Parking when moving |
| 63 | Movement |  | Advance to the nearest coloured property owned by another player |
| 64 | Movement |  | Advance to Bond Street |
| 65 | Movement |  | Advance to Euston Road |
| 66 | Movement |  | Advance 8 spaces |
| 67 | Movement |  | Swap places with any player of your choice |
| 68 | Movement | ✓ | Advance 6 spaces |
| 69 | Movement | ✓ | Advance 2 spaces |
| 70 | Movement | ✓ | After next move, move forwards 23 spaces |
| 71 | Movement | ✓ | Advance to Go! |
| 72 | Movement | ✓ | Advance 1 space |
| 73 | Movement | ✓ | Advance 1 space |
| 74 | Movement | ✓ | Advance up to 5 spaces |
| 79 ⚠ | Property |  | Swap 1 of your properties with any property of any player - excluding properties with buildings |
| 79 ⚠ | Buildings | ✓ | When you get a new set you will receive 1 house for each property in the set. |
| 80 | Money |  | Your cost to leave Jail is reset to £50 |
| 81 | Property |  | A corrupt judge steals 1 of your properties and puts it into Free Parking. It is not recorded |
| 82 | Property |  | You must give 1 property to the player who rolls the highest with 1 dice |
| 83 | Property |  | You must put 1 property in Free Parking. It is not recorded. |
| 84 | Property |  | Return 1 property to the bank |
| 85 | Property |  | Hand in any property to Free Parking. It does not get recorded and could have been handed in earlier. |
| 86 | Money |  | You have sold your car. Collect £2,000 |
| 87 | Money |  | You have won the lottery. Collect £3,000 |
| 88 | Money |  | Your book sales earn you £2,000 |
| 89 | Money |  | Wrongful arrest! Counter law suit wins you £2,000 |
| 90 | Money |  | Happy New Year! Collect £300 from each player |
| 91 | Money |  | Bank interest. Receive £500 |
| 92 | Money |  | You inherit £1,000 |
| 93 | Money |  | From the sale of shares you receive £1,500 |
| 94 | Money |  | You find £500 in the street |
| 95 | Money |  | Insurance claim successful. Collect £300 |
| 96 | Money |  | Every other player must pay the lander £1,000 %age. No other card can be played. |
| 97 | Money | ✓ | Former prisoner agrees to steal the £200 bonus for passing Go! From other players. Valid 10 times |
| 98 | Money |  | Escaping prisoner drops £500, finders keepers! |
| 99 | Money | ✓ | You befriend a prison guard. All payments to leave jail you receive until you go to jail |
| 100 | Money |  | Tax error. Receive what you have to pay. |
| 101 | Money |  | Tax refund is multiplied by 1 dice |
| 102 | Money | ✓ | When moving anti-clockwise, receive £200 for passing Go! Valid for 5 times |
| 103 | Money | ✓ | When moving anti-clockwise, receive £500 for passing Go! Valid for 4 times |
| 104 | Money |  | Receive £200 times 1 dice |
| 105 | Money |  | All players receive £200 times 2 dice. %age applies |
| 106 | Money |  | Your triple bonus is multiplied by the roll of 1 dice |
| 107 | Money |  | Each player rolls 1 dice. The player(s) with the highest number get the triple bonus |
| 108 | Money |  | Your triple bonus is doubled |
| 109 | Money | ✓ | Receive the money from Free Parking that another player would have received. |
| 110 | Money | ✓ | Your money for landing on Go! Is doubled for the next 5 occasions |
| 111 | Money |  | Hidden treasure! Collect £1,000 from the bank or £300 from each player |
| 112 | Money | ✓ | Pick a set of any player. For 10 turns you receive 50% of any rents due |
| 113 ⚠ | Money |  | Your double is converted into a triple |
| 113 ⚠ | Downgrade |  | Your double is converted into a triple |
| 114 ⚠ | Downgrade | ✓ | Dodgy judge facilitates a double in jail to turn into a triple. No triple card to be taken. |
| 114 ⚠ | Jail | ✓ | You know a corrupt prison guard. The next time you are in jail you will receive rent |
| 115 | Card |  | Steal any card from any player |
| 116 | Property |  | Choose any available property from the bank |
| 117 | Money | ✓ | The next time you land on Free Parking you receive all the money |
| 118 | Jail | ✓ | Send any player of your choice to jail |
| 119 | Jail |  | Swap any player with any player in jail |
| 120 | Jail | ✓ | Get out of jail free |
| 121 | Jail |  | Mass breakout from prison. All prisoners escape. |
| 122 | Jail | ✓ | Get out of jail free |
| 123 | Jail | ✓ | Get out of jail free |
| 124 | Jail | ✓ | Get out of jail free |
| 125 | Jail | ✓ | Get out of jail free |
| 126 | Jail |  | Send a player of your choice to jail |
| 127 | Jail |  | All players go immediately to jail |
| 129 | Money |  | Income tax refund collect £50 |
| 130 | Money |  | Pay a £50 fine or take a chance |
| 131 | Money |  | Receive interest on your preference shares of £50 |
| 132 | Money |  | Doctors fee, pay £50 |
| 133 | Money |  | It is your birthday, collect £50 from each player |
| 134 | Movement |  | Go back to Old Kent Road |
| 135 | Movement |  | Go to jail |
| 136 | Movement |  | Advance to Go! |
| 137 | Money |  | Pay hospital £100 |
| 138 | Money |  | You have won second prize in a beauty competition, collect £50 |
| 139 | Money |  | You inherit £100 |
| 140 | Money |  | Bank error in your favour, collect £200 |
| 141 | Money |  | Pay your insurance premium £50 |
| 142 | Money |  | Your building loan matures, receive £150 |
| 143 | Money |  | From sale of stock you get £50 |
| 144 | Movement |  | Advance to Go! |
| 145 | Movement |  | Go back 3 spaces |
| 146 | Money |  | Drunk in charge, fine £20 |
| 147 | Money |  | You have won a crossword competition, collect £100 |
| 148 | Movement |  | Take a trip to Marylebone Station, do not pass Go! |
| 149 | Movement |  | Advance to Pall Mall |
| 150 | Money |  | Pay school fees of £150 |
| 153 | Movement |  | Go to jail |
| 154 | Movement |  | Advance to Trafalgar Square |
| 157 | Movement |  | Advance to Mayfair |
| 158 | Money |  | Bank pays you dividend of £50 |
| 159 | Money |  | Speeding fine £50 |
| 160 | Money |  | Annuity matures, collect £100 |
| 161 | Movement | ✓ | Go to jail for 10 turns. You can roll the dice but cannot leave jail. Can collect all rents due |
| 163 | Money | ✓ | Pay each player £300 the next time you alnd on Go! |
| 165 | Money |  | Swap all your money with the player rolling lowest with 1 dice |
| 166 | Money |  | For this turn and the next 5, roll 1 dice times £100. Take the money from Free Parking. From the bank if insufficent there |
| 185 | Movement |  | You call a meeting. All other players not in jail move to Just Visiting. They do not take a card. |
| 186 | Jail |  | As a regular offender your jail term is extended by the number on the dice which sent you. |
| 187 | Movement |  | Mishandled evidence. Do not go to jail, move to Just Visiting |
| 196 | Money |  | The tax payment is doubled and every player pays |
| 205 | Money |  | Bonanza! All players receive £3,000 from the bank |
| 206 | Money | ✓ | You open a car park at Free Parking. Receive all the payments to Free Parking until any player rolls a double. |
| 209 | Money | ✓ | Rail Strike! There is no rent on the stations until any player rolls a double |
| 210 | Money | ✓ | Energy crisis! Rent from utilities is multiplied by 10 until any player rolls a double |
| 211 | Money | ✓ | You become the Taxman until someone goes to jail. You receive all the money from the tax spaces and the bank pays out any tax refunds due |
| 216 | Movement |  | ID check fails! Swap places with a player of your choice |

---

## Actions (all rows, by No.)

| No. | Category | Keep | Conditions | Link | Instructions |
|---|---|---|---|---|---|
| 1 | Change Direction | ✓ | Choice·Self | — | Change direction |
| 2 | Turns |  | — | — | Miss 3 turns |
| 3 | Loans |  | — | 4 | All outstanding loans are wiped out |
| 4 | Money |  | — | 5 | Any player with no loan receives £1,000 |
| 5 | Property |  | — | 4 | Must return a property to the bank |
| 6 | Change Direction |  | — | — | each player changes directions including those in jail |
| 7 | Turns |  | — | — | Have an extra 3 turns |
| 8 | Turns |  | — | 9 | Miss 3 turns or |
| 9 | Turns |  | — | 8 | have an extra 3 turns |
| 10 | Downgrade | ✓ | Met·Self | — | Your next triple is downgraded to a double |
| 11 | Downgrade | ✓ | Choice·Self | — | Converet a double into a triple |
| 12 | Downgrade | ✓ | Choice·Other | — | Cancel a player's triple bonus |
| 16 | Card |  | — | 17 | Pass any retained card to the player rolling the lowest number with 1 dice |
| 17 | Card |  | — | 16 | 1 card to each player who rolls the lowest number |
| 18 | Money |  | — | — | Your credit rating plummets! Your outstanding loan has been increased by £3,000 |
| 19 | Buildings |  | — | — | A friend in jail arranges for someone to purge 2 properties of your choice |
| 20 | Buildings |  | — | 21 | Bad news! Purge 1 of your properties |
| 21 | Property |  | — | 20 | Also hand back a property to the bank |
| 23 | Movement |  | — | — | Go back to Bow Street |
| 24 | Movement |  | — | — | Go back to income tax |
| 25 | Movement |  | — | — | Go back 11 spaces |
| 26 | Movement | ✓ | Met·Self | — | After next roll, go back 17 spaces |
| 28 | Movement |  | — | — | Go back 1 space |
| 30 | Movement |  | — | — | Go back to Income Tax and pay £200 |
| 31 | Immunity | ✓ | Choice·Self | — | Immunity from triple bonus being cancelled, play once |
| 32 | Immunity | ✓ | Choice·Self; Choice·Other | — | Immunity from swapping all money with another player |
| 33 | Immunity | ✓ | Choice·Self | — | Immunity from any card drawn when landing on go to to jail, play once |
| 34 | Immunity | ✓ | Choice·Self | — | Immunity from any Go To Jail card, play once. |
| 35 | Money |  | — | — | You have been robbed. Pay £1,000 to the player rolling the highest with 1 dice. |
| 36 | Money |  | — | — | Pay university fees of £1,000 |
| 37 | Money |  | — | 38 | You are assessed for street repairs, pay £80 for each house |
| 38 | Money |  | — | 37 | Pay £230 for each hotel |
| 39 | Money |  | — | — | You fail a breathalyser test. Pay £500 |
| 40 | Money |  | — | 41 | Make general repairs on all of your buildings. Pay £50 per house |
| 41 | Money |  | — | 40 | Pay £200 for each hotel |
| 42 | Money |  | — | — | Speeding fine. Pay £300 |
| 43 | Money |  | — | 44 | Pay each player £100 and |
| 44 | Jail |  | — | 43 | Go to jail |
| 45 | Money |  | — | 46 | Pay each player £5,000 - %age applies and |
| 46 | Jail |  | — | 45 | Go to jail |
| 47 | Money |  | — | — | Fine of £1,000 imposed by the judge, %age applies. Do not go to Jail or Just Visiting |
| 48 | Money |  | — | — | You make a donation to charity. Pay £100 per property that you own. |
| 49 | Money |  | — | — | £400 fine or take a %age Chance |
| 50 | Money |  | — | — | Burst tyre, pay £200 |
| 51 | Money |  | — | 52 | You have been robbed by an escaping prisoner. Pay £300 to Free Parking and |
| 52 | Property |  | — | 51 | Return 1 property to the bank |
| 53 | Money |  | — | — | The tax payment is tripled |
| 54 | Money |  | — | — | The tax payment is halved |
| 55 | Money |  | — | — | The tax is to bepaid by the player rolling the lowest number with 1 dice |
| 56 | Money | ✓ | Met·Self | — | Unlucky! No money for landing on Go! For 5 turns including this turn |
| 57 | Money |  | — | — | You receive no money for the Triple |
| 58 | Money | ✓ | Met·Self | — | You receive no cash on your next visit to Free Parking |
| 59 | Money | ✓ | Met·Self | — | Your next payment to another player is doubled |
| 62 | Movement |  | — | — | #Advance to any space on the board without being able to land on Go! Or Free Parking when moving |
| 63 | Movement |  | — | — | Advance to the nearest coloured property owned by another player |
| 64 | Movement |  | — | — | Advance to Bond Street |
| 65 | Movement |  | — | — | Advance to Euston Road |
| 66 | Movement |  | — | — | Advance 8 spaces |
| 67 | Movement |  | — | — | Swap places with any player of your choice |
| 68 | Movement | ✓ | Choice·Self | — | Advance 6 spaces |
| 69 | Movement | ✓ | Choice·Self | — | Advance 2 spaces |
| 70 | Movement | ✓ | Met·Self | — | After next move, move forwards 23 spaces |
| 71 | Movement | ✓ | Choice·Self | — | Advance to Go! |
| 72 | Movement | ✓ | Choice·Self | — | Advance 1 space |
| 73 | Movement | ✓ | Choice·Self | — | Advance 1 space |
| 74 | Movement | ✓ | Choice·Self | — | Advance up to 5 spaces |
| 77 | Property |  | — | 78 | Swap a set with any player and |
| 78 | Buildings |  | — | 77 | Both sets get purged |
| 79 | Buildings | ✓ | Met·Self | — | When you get a new set you will receive 1 house for each property in the set. |
| 79 | Property |  | — | — | Swap 1 of your properties with any property of any player - excluding properties with buildings |
| 80 | Money |  | — | — | Your cost to leave Jail is reset to £50 |
| 81 | Property |  | — | — | A corrupt judge steals 1 of your properties and puts it into Free Parking. It is not recorded |
| 82 | Property |  | — | — | You must give 1 property to the player who rolls the highest with 1 dice |
| 83 | Property |  | — | — | You must put 1 property in Free Parking. It is not recorded. |
| 84 | Property |  | — | — | Return 1 property to the bank |
| 85 | Property |  | — | — | Hand in any property to Free Parking. It does not get recorded and could have been handed in earlier. |
| 86 | Money |  | — | — | You have sold your car. Collect £2,000 |
| 87 | Money |  | — | — | You have won the lottery. Collect £3,000 |
| 88 | Money |  | — | — | Your book sales earn you £2,000 |
| 89 | Money |  | — | — | Wrongful arrest! Counter law suit wins you £2,000 |
| 90 | Money |  | — | — | Happy New Year! Collect £300 from each player |
| 91 | Money |  | — | — | Bank interest. Receive £500 |
| 92 | Money |  | — | — | You inherit £1,000 |
| 93 | Money |  | — | — | From the sale of shares you receive £1,500 |
| 94 | Money |  | — | — | You find £500 in the street |
| 95 | Money |  | — | — | Insurance claim successful. Collect £300 |
| 96 | Money |  | — | — | Every other player must pay the lander £1,000 %age. No other card can be played. |
| 97 | Money | ✓ | Met·Other | — | Former prisoner agrees to steal the £200 bonus for passing Go! From other players. Valid 10 times |
| 98 | Money |  | — | — | Escaping prisoner drops £500, finders keepers! |
| 99 | Money | ✓ | Met·Self | — | You befriend a prison guard. All payments to leave jail you receive until you go to jail |
| 100 | Money |  | — | — | Tax error. Receive what you have to pay. |
| 101 | Money |  | — | — | Tax refund is multiplied by 1 dice |
| 102 | Money | ✓ | Met·Self | — | When moving anti-clockwise, receive £200 for passing Go! Valid for 5 times |
| 103 | Money | ✓ | Met·Self | — | When moving anti-clockwise, receive £500 for passing Go! Valid for 4 times |
| 104 | Money |  | — | — | Receive £200 times 1 dice |
| 105 | Money |  | — | — | All players receive £200 times 2 dice. %age applies |
| 106 | Money |  | — | — | Your triple bonus is multiplied by the roll of 1 dice |
| 107 | Money |  | — | — | Each player rolls 1 dice. The player(s) with the highest number get the triple bonus |
| 108 | Money |  | — | — | Your triple bonus is doubled |
| 109 | Money | ✓ | Choice·Self | — | Receive the money from Free Parking that another player would have received. |
| 110 | Money | ✓ | Met·Self | — | Your money for landing on Go! Is doubled for the next 5 occasions |
| 111 | Money |  | — | — | Hidden treasure! Collect £1,000 from the bank or £300 from each player |
| 112 | Money | ✓ | Met·Other | — | Pick a set of any player. For 10 turns you receive 50% of any rents due |
| 113 | Downgrade |  | — | — | Your double is converted into a triple |
| 113 | Money |  | — | — | Your double is converted into a triple |
| 114 | Downgrade | ✓ | Met·Self | — | Dodgy judge facilitates a double in jail to turn into a triple. No triple card to be taken. |
| 114 | Jail | ✓ | Met·Self | — | You know a corrupt prison guard. The next time you are in jail you will receive rent |
| 115 | Card |  | — | — | Steal any card from any player |
| 116 | Property |  | — | — | Choose any available property from the bank |
| 117 | Money | ✓ | Met·Self | — | The next time you land on Free Parking you receive all the money |
| 118 | Jail | ✓ | Choice·Self | — | Send any player of your choice to jail |
| 119 | Jail |  | — | — | Swap any player with any player in jail |
| 120 | Jail | ✓ | Choice·Self | — | Get out of jail free |
| 121 | Jail |  | — | — | Mass breakout from prison. All prisoners escape. |
| 122 | Jail | ✓ | Choice·Self | — | Get out of jail free |
| 123 | Jail | ✓ | Choice·Self | — | Get out of jail free |
| 124 | Jail | ✓ | Choice·Self | — | Get out of jail free |
| 125 | Jail | ✓ | Choice·Self | — | Get out of jail free |
| 126 | Jail |  | — | — | Send a player of your choice to jail |
| 127 | Jail |  | — | — | All players go immediately to jail |
| 129 | Money |  | — | — | Income tax refund collect £50 |
| 130 | Money |  | — | — | Pay a £50 fine or take a chance |
| 131 | Money |  | — | — | Receive interest on your preference shares of £50 |
| 132 | Money |  | — | — | Doctors fee, pay £50 |
| 133 | Money |  | — | — | It is your birthday, collect £50 from each player |
| 134 | Movement |  | — | — | Go back to Old Kent Road |
| 135 | Movement |  | — | — | Go to jail |
| 136 | Movement |  | — | — | Advance to Go! |
| 137 | Money |  | — | — | Pay hospital £100 |
| 138 | Money |  | — | — | You have won second prize in a beauty competition, collect £50 |
| 139 | Money |  | — | — | You inherit £100 |
| 140 | Money |  | — | — | Bank error in your favour, collect £200 |
| 141 | Money |  | — | — | Pay your insurance premium £50 |
| 142 | Money |  | — | — | Your building loan matures, receive £150 |
| 143 | Money |  | — | — | From sale of stock you get £50 |
| 144 | Movement |  | — | — | Advance to Go! |
| 145 | Movement |  | — | — | Go back 3 spaces |
| 146 | Money |  | — | — | Drunk in charge, fine £20 |
| 147 | Money |  | — | — | You have won a crossword competition, collect £100 |
| 148 | Movement |  | — | — | Take a trip to Marylebone Station, do not pass Go! |
| 149 | Movement |  | — | — | Advance to Pall Mall |
| 150 | Money |  | — | — | Pay school fees of £150 |
| 151 | Money |  | — | 152 | You are assessed for street repairs, pay £50 per house |
| 152 | Money |  | — | 151 | Pay £100 per hotel |
| 153 | Movement |  | — | — | Go to jail |
| 154 | Movement |  | — | — | Advance to Trafalgar Square |
| 155 | Money |  | — | 156 | Make general repairs on all of your buildings. Pay £50 per house |
| 156 | Money |  | — | 155 | Pay £100 per hotel |
| 157 | Movement |  | — | — | Advance to Mayfair |
| 158 | Money |  | — | — | Bank pays you dividend of £50 |
| 159 | Money |  | — | — | Speeding fine £50 |
| 160 | Money |  | — | — | Annuity matures, collect £100 |
| 161 | Movement | ✓ | Choice·Self | — | Go to jail for 10 turns. You can roll the dice but cannot leave jail. Can collect all rents due |
| 163 | Money | ✓ | Met·Self | — | Pay each player £300 the next time you alnd on Go! |
| 165 | Money |  | — | — | Swap all your money with the player rolling lowest with 1 dice |
| 166 | Money |  | — | — | For this turn and the next 5, roll 1 dice times £100. Take the money from Free Parking. From the bank if insufficent there |
| 167 | Movement |  | — | 168 | Advance to Go!, do not take a card and |
| 168 | Money |  | — | 167 | Collect £200 and |
| 169 | Money |  | — | 168 | All other players receive £100 times 1 dice |
| 170 | Money |  | — | 171 | Recive the bonus and |
| 171 | Movement | ✓ | Met·Self | 170 | Go to jail for 5 turns |
| 172 | Money |  | — | 173 | Receive the bonus but must hand back have of your existing cash or |
| 173 | Property |  | — | 172 | Or return a set to the bank |
| 174 | Money |  | — | 175 | You must make a choice, no bonus or |
| 175 | Money |  | — | 176 | receive the bonus plus |
| 176 | Property |  | — | 175 | Plus return two properties to the bank |
| 177 | Movement |  | — | 178 | Advance to Go!, do not take a Go! Card and |
| 178 | Money |  | — | 177 | and roll 2 dice times £100 |
| 179 | Money |  | — | 180 | You steal £200 from each player and the bank and |
| 180 | Movement |  | — | 179 | and go to jail |
| 181 | Movement |  | — | 182 | Advance to the nearest station owned by someone else but |
| 182 | Turns |  | — | 181 | but miss a turn |
| 183 | Movement |  | — | 184 | Swap places with another in jail. Other players roll to see who it is and |
| 184 | Money |  | — | 183 | and you also swap the amounts to come out of jail |
| 185 | Movement |  | — | — | You call a meeting. All other players not in jail move to Just Visiting. They do not take a card. |
| 186 | Jail |  | — | — | As a regular offender your jail term is extended by the number on the dice which sent you. |
| 187 | Movement |  | — | — | Mishandled evidence. Do not go to jail, move to Just Visiting |
| 188 | Movement |  | — | 189 | Swap places with any other player and |
| 189 | Money |  | — | 188 | and both players receive £200 |
| 190 | Money |  | — | 191 | Roll 2 dice and |
| 191 | Money |  | — | 192 | and multiply the total by the third dice and |
| 192 | Money |  | — | 193 | and receive the final total times £200 %age applies and |
| 193 | Property |  | — | 192 | must return 1 or 2 or properties to the bank |
| 194 | Money |  | — | 195 | Pay the tax that is due but |
| 195 | Buildings |  | — | 194 | but receive a free hotel which has to be the next hotel you erect |
| 196 | Money |  | — | — | The tax payment is doubled and every player pays |
| 197 | Money |  | — | 198 | Pay double or |
| 198 | Money |  | — | 199 | Pay half and |
| 199 | Movement |  | — | 198 | and go to jail |
| 200 | Money |  | — | 201 | Pay the tax and roll 1 dice and |
| 201 | Property |  | — | 200 | and if even, receive a property from Free Parking, if none |
| 202 | Property |  | — | 203 | Recive a property from the bank, if none |
| 203 | Money |  | — | 204 | if none, the bank pays you £3,000 and |
| 204 | Property |  | — | — | if odd, put a property into Free Parking. It is not recordded |
| 205 | Money |  | — | — | Bonanza! All players receive £3,000 from the bank |
| 206 | Money | ✓ | Met·Other | — | You open a car park at Free Parking. Receive all the payments to Free Parking until any player rolls a double. |
| 207 | Money |  | — | 208 | Collect £500 and |
| 208 | Movement |  | — | 207 | and do not turn around |
| 209 | Money | ✓ | Met·Other | — | Rail Strike! There is no rent on the stations until any player rolls a double |
| 210 | Money | ✓ | Met·Other | — | Energy crisis! Rent from utilities is multiplied by 10 until any player rolls a double |
| 211 | Money | ✓ | Met·Other | — | You become the Taxman until someone goes to jail. You receive all the money from the tax spaces and the bank pays out any tax refunds due |
| 212 | Property |  | — | 213 | Return a set to the bank or |
| 213 | Money |  | — | 212 | or pay £100 times the number of properties you own |
| 214 | Money |  | — | 215 | Your lucky day! Proceed as normal but first take £1,500 if |
| 215 | Money |  | — | — | if there is a shortfall it is paid by the bank, other cards can then be played |
| 216 | Movement |  | — | — | ID check fails! Swap places with a player of your choice |

---

## Derived triggers (`TriggerEvent`)

The sheet's columns give the **`ConditionType`** (Keep / Met·Self / Met·Other / Choice·Self /
Choice·Other — `cards-design.md` §5) but **not** the *event* a held card waits on — that lives in
the **instruction text**. Below it's distilled into the `TriggerEvent` set (`cards-design.md` §5,
§11.3).

Only **keep-until-needed** cards carry a trigger: a non-keep card is `ConditionType.None`,
resolved the moment it's drawn (its only "trigger" is being drawn from its `CardType` deck —
§4b). So the triggers are derived from the keep rows above. The big leverage holds: nearly every
trigger lands on a branch the engine **already** cites (`RuleCode`), so the held-card hook rides
the existing `CiteRule` points rather than a new event bus.

### Play triggers — the events that make a held card live

| Trigger | From actions | Engine hook (existing `RuleCode` / site) |
|---|---|---|
| `OnLandGo` | 56, 110, 163 | `Go_LandOn` (`GoService.LandOnGo`) |
| `OnPassGo` *(param: direction)* | 102, 103 *(anti-clockwise)* | `Go_PassClockwise` / `Go_PassAntiClockwise` (`GoService.CollectGoMoney`) |
| `OnOtherPassGo` | 97 | `CollectGoMoney` during another player's third-die move |
| `OnLandFreeParking` | 58, 117 | `FreeParking_*` (`FreeParkingService.ProcessFreeParking`) |
| `OnOtherTakesFreeParking` | 109 | `FreeParking_TakeCap` (another player's FP take) |
| `OnRollDouble` | 11, 114 *(Downgrade — in jail)* | `Double_*` (orchestrator double branch) |
| `OnRollTriple` | 10 | `Triple_*` (orchestrator triple branch) |
| `OnOtherRollsTriple` | 12 | `Triple_Bonus` on another player |
| `OnEnterJail` | 114 *(Jail — "next time in jail")* | `GoToJail_SendToJail` / `Double_ThreeInRowToJail` / `Triple_ThreeInRowToJail` |
| `OnInJail` *(to leave)* | 120, 122, 123, 124, 125 | `Jail_LeaveByDouble` / leave-jail command path |
| `OnPayPlayer` | 59 | player→player `FinancialTransactionReceipt` |
| `OnRentDue` | 112 | rent path (`PropertyService.PayPropertyRent`) |
| `OnNextRoll` | 26 | post-roll, orchestrator |
| `OnNextMove` | 70 | post-move, `MovementService` |
| `OnCompleteSet` | 79 | `CheckReservationRuleSetObtained` / set completion |

**`Anytime` (own turn)** — actions 1, 68, 69, 71, 72, 73, 74, 118, 161 are `Choice·Self` with **no
event gate**: playable at the holder's discretion on their own turn. That's `ConditionType.ChoiceCardholderTurn`
with **no** `TriggerEvent` (i.e. `None`) — the choice window *is* the gate, not an event.

```csharp
[Flags]
public enum TriggerEvent
{
    None                   = 0,        // resolve-on-draw, or "anytime on own turn"
    OnLandGo               = 1 << 0,
    OnPassGo               = 1 << 1,   // + direction param (anti-clockwise: 102/103)
    OnOtherPassGo          = 1 << 2,
    OnLandFreeParking      = 1 << 3,
    OnOtherTakesFreeParking= 1 << 4,
    OnRollDouble           = 1 << 5,
    OnRollTriple           = 1 << 6,
    OnOtherRollsTriple     = 1 << 7,
    OnEnterJail            = 1 << 8,
    OnInJail               = 1 << 9,
    OnPayPlayer            = 1 << 10,
    OnRentDue              = 1 << 11,
    OnNextRoll             = 1 << 12,
    OnNextMove             = 1 << 13,
    OnCompleteSet          = 1 << 14,
}
```

### Two adjacent axes — *not* `TriggerEvent`s

Surfaced in the same texts but a different concern; flagged so they don't get folded into the
trigger enum by mistake:

1. **Effect-lifetime / expiry** — *when an ongoing effect ends*, not when a card plays:
   - **Until any player rolls a double** — 209, 210 (and the global-event clear, §7).
   - **Charge / turn counters** — "valid N times" (97, 102, 103), "for N turns" (56, 110, 112).
   These want a small **duration** model on the *effect* (a countdown / end-condition), separate
   from the play trigger.

2. **Reactive counters (Immunity / NOPE — `cards-design.md` §6)** — held cards played to *stop an
   action aimed at you*, keyed to the **action type countered**, not to a board event:
   - 31 — immunity from triple-bonus cancel.
   - 32 — immunity from money-swap.
   - 33, 34 — immunity from a Go-To-Jail card.
   These ride the counter window (§6), not the `TriggerEvent` hook.

### Open / to confirm
- **`OnPassGo` direction** — model anti-clockwise as a parameter on the condition (102/103) rather
  than a separate flag, per the parameterised-`CardCondition` plan (`cards-design.md` §5).
- **`OnRentDue` granularity** — 112 scopes to a *chosen set*; 209/210 scope to *stations* / *utilities*.
  The trigger is "rent due"; the scope (which property/kind) is an effect parameter.
- **`OnEnterJail` vs `OnInJail`** — entering jail (114-Jail arms a future effect) vs being in jail
  (120–125 leave) are distinct moments; kept as two triggers.
- **Action 171** (`Met·Self`, "go to jail for 5 turns", linked to 170) reads as part of a
  multi-action card, not a standalone trigger — confirm against the card it belongs to.
