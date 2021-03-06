﻿using System;
using static PKHeX.Core.LegalityCheckStrings;

namespace PKHeX.Core
{
    /// <summary>
    /// Verifies the <see cref="PKM.Nickname"/>.
    /// </summary>
    public sealed class NicknameVerifier : Verifier
    {
        protected override CheckIdentifier Identifier => CheckIdentifier.Nickname;

        public override void Verify(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var EncounterMatch = data.EncounterMatch;

            // If the Pokémon is not nicknamed, it should match one of the language strings.
            if (pkm.Nickname.Length == 0)
            {
                data.AddLine(GetInvalid(V2));
                return;
            }
            if (pkm.Species > PKX.SpeciesLang[0].Length)
            {
                data.AddLine(Get(V2, Severity.Indeterminate));
                return;
            }

            if (pkm.VC && pkm.IsNicknamed)
            {
                VerifyG1NicknameWithinBounds(data, pkm.Nickname);
            }
            else if (EncounterMatch is MysteryGift m)
            {
                if (pkm.IsNicknamed && !m.IsEgg)
                   data.AddLine(Get(V0, Severity.Fishy));
            }

            if (EncounterMatch is EncounterTrade)
            {
                VerifyNicknameTrade(data);
                return;
            }

            if (pkm.IsEgg)
            {
                VerifyNicknameEgg(data);
                return;
            }

            string nickname = pkm.Nickname.Replace("'", "’");
            if (VerifyUnNicknamedEncounter(data, pkm, nickname))
                return;

            // Non-nicknamed strings have already been checked.
            if (Legal.CheckWordFilter && pkm.IsNicknamed && WordFilter.IsFiltered(nickname, out string bad))
                data.AddLine(GetInvalid($"Wordfilter: {bad}"));
        }

        private bool VerifyUnNicknamedEncounter(LegalityAnalysis data, PKM pkm, string nickname)
        {
            if (pkm.IsNicknamed)
            {
                for (int i = 0; i < PKX.SpeciesLang.Length; i++)
                {
                    if (!PKX.SpeciesDict[i].TryGetValue(nickname, out int index))
                        continue;
                    var msg = index == pkm.Species && i != pkm.Language ? V15 : V16;
                    data.AddLine(Get(msg, Severity.Fishy));
                    return true;
                }
                if (StringConverter.HasEastAsianScriptCharacters(nickname)) // East Asian Scripts
                {
                    data.AddLine(GetInvalid(V222));
                    return true;
                }
                data.AddLine(GetValid(V17));
            }
            else if (pkm.Format < 3)
            {
                // pk1/pk2 IsNicknamed getter checks for match, logic should only reach here if matches.
                data.AddLine(GetValid(V18));
            }
            else
            {
                var EncounterMatch = data.EncounterMatch;
                // Can't have another language name if it hasn't evolved or wasn't a language-traded egg.
                bool evolved = EncounterMatch.Species != pkm.Species;
                bool match = PKX.GetSpeciesNameGeneration(pkm.Species, pkm.Language, pkm.Format) == nickname;
                if (pkm.WasTradedEgg || evolved)
                    match |= !PKX.IsNicknamedAnyLanguage(pkm.Species, nickname, pkm.Format);
                if (!match && pkm.Format == 5 && !pkm.IsNative) // transfer
                {
                    if (evolved)
                        match |= !PKX.IsNicknamedAnyLanguage(pkm.Species, nickname, 4);
                    else
                        match |= PKX.GetSpeciesNameGeneration(pkm.Species, pkm.Language, 4) == nickname;
                }

                if (!match)
                {
                    if (EncounterMatch is WC7 wc7 && wc7.IsAshGreninjaWC7(pkm))
                        data.AddLine(GetValid(V19));
                    else
                        data.AddLine(GetInvalid(V20));
                }
                else
                {
                    data.AddLine(GetValid(V18));
                }
            }
            return false;
        }

        private void VerifyNicknameEgg(LegalityAnalysis data)
        {
            var Info = data.Info;
            var pkm = data.pkm;
            var EncounterMatch = Info.EncounterMatch;
            switch (pkm.Format)
            {
                case 4:
                    if (pkm.IsNicknamed) // gen4 doesn't use the nickname flag for eggs
                        data.AddLine(GetInvalid(V224, CheckIdentifier.Egg));
                    break;
                case 7:
                    if (EncounterMatch is EncounterStatic ^ !pkm.IsNicknamed) // gen7 doesn't use for ingame gifts
                        data.AddLine(GetInvalid(pkm.IsNicknamed ? V224 : V12, CheckIdentifier.Egg));
                    break;
                default:
                    if (!pkm.IsNicknamed)
                        data.AddLine(GetInvalid(V12, CheckIdentifier.Egg));
                    break;
            }

            if (pkm.Format == 2 && pkm.IsEgg && !PKX.IsNicknamedAnyLanguage(0, pkm.Nickname, 2))
                data.AddLine(GetValid(V14, CheckIdentifier.Egg));
            else if (PKX.GetSpeciesNameGeneration(0, pkm.Language, Info.Generation) != pkm.Nickname)
                data.AddLine(GetInvalid(V13, CheckIdentifier.Egg));
            else
                data.AddLine(GetValid(V14, CheckIdentifier.Egg));
        }

        private void VerifyNicknameTrade(LegalityAnalysis data)
        {
            switch (data.Info.Generation)
            {
                case 1:
                case 2: VerifyTrade12(data); return;
                case 3: VerifyTrade3(data); return;
                case 4: VerifyTrade4(data); return;
                case 5: VerifyTrade5(data); return;
                case 6: VerifyTrade6(data); return;
                case 7: VerifyTrade7(data); return;
            }
        }

        private void VerifyG1NicknameWithinBounds(LegalityAnalysis data, string str)
        {
            var pkm = data.pkm;
            if (StringConverter.GetIsG1English(str))
            {
                if (str.Length > 10)
                    data.AddLine(GetInvalid(V1));
            }
            else if (StringConverter.GetIsG1Japanese(str))
            {
                if (str.Length > 5)
                    data.AddLine(GetInvalid(V1));
            }
            else if (pkm.Korean && StringConverter.GetIsG2Korean(str))
            {
                if (str.Length > 5)
                    data.AddLine(GetInvalid(V38));
            }
            else
            {
                data.AddLine(GetInvalid(V422));
            }
        }

        private void VerifyTrade12(LegalityAnalysis data)
        {
            var et = (EncounterTrade)data.EncounterOriginal;
            if (et.TID != 0) // Gen2 Trade
                return; // already checked all relevant properties when fetching with getValidEncounterTradeVC2

            if (!EncounterGenerator.IsEncounterTrade1Valid(data.pkm, et))
                data.AddLine(GetInvalid(V10, CheckIdentifier.Trainer));
        }

        private void VerifyTrade3(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var EncounterMatch = data.EncounterMatch;
            if (pkm.FRLG)
            {
                int lang = pkm.Language;
                if (EncounterMatch.Species == 124) // Jynx
                    lang = DetectTradeLanguageG3DANTAEJynx(pkm, lang);
                VerifyTradeTable(data, Encounters3.TradeFRLG, Encounters3.TradeGift_FRLG, lang);
            }
            else
            {
                VerifyTradeTable(data, Encounters3.TradeRSE, Encounters3.TradeGift_RSE);
            }
        }

        private void VerifyTrade4(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var EncounterMatch = data.EncounterMatch;
            if (pkm.TID == 1000)
            {
                VerifyTrade4Ranch(data);
                return;
            }
            if (pkm.HGSS)
            {
                int lang = pkm.Language;
                if (EncounterMatch.Species == 25) // Pikachu
                {
                    lang = DetectTradeLanguageG4SurgePikachu(pkm, lang);
                    // flag korean magikarp on gen4 saves since the pkm.Language is German
                    if (pkm.Format == 4 && lang == (int)LanguageID.Korean && Legal.ActiveTrainer.Language != (int)LanguageID.Korean && Legal.ActiveTrainer.Language >= 0)
                        data.AddLine(GetInvalid(string.Format(V610, V611, V612), CheckIdentifier.Language));
                }
                VerifyTradeTable(data, Encounters4.TradeHGSS, Encounters4.TradeGift_HGSS, lang);
            }
            else // DPPt
            {
                int lang = pkm.Language;
                if (EncounterMatch.Species == 129) // Magikarp
                {
                    lang = DetectTradeLanguageG4MeisterMagikarp(pkm, lang);
                    // flag korean magikarp on gen4 saves since the pkm.Language is German
                    if (pkm.Format == 4 && lang == (int)LanguageID.Korean && Legal.ActiveTrainer.Language != (int)LanguageID.Korean && Legal.ActiveTrainer.Language >= 0)
                        data.AddLine(GetInvalid(string.Format(V610, V611, V612), CheckIdentifier.Language));
                }
                else if (!pkm.Pt && lang == 1) // DP English origin are Japanese lang
                {
                    int index = Array.IndexOf(Encounters4.TradeGift_DPPt, data.EncounterMatch);
                    if (Encounters4.TradeDPPt[1][index] != pkm.Nickname) // not japanese
                        lang = 2; // English
                }
                VerifyTradeTable(data, Encounters4.TradeDPPt, Encounters4.TradeGift_DPPt, lang);
            }
        }

        private static int DetectTradeLanguageG3DANTAEJynx(PKM pk, int lang)
        {
            if (lang != (int)LanguageID.Italian)
                return lang;

            if (pk.Version == (int)GameVersion.LG)
                lang = (int)LanguageID.English; // translation error; OT was not localized => same as English
            return lang;
        }

        private static int DetectTradeLanguageG4MeisterMagikarp(PKM pkm, int lang)
        {
            if (lang == (int)LanguageID.English)
                return (int)LanguageID.German;

            // All have German, regardless of origin version.
            // Detect which language they originated from... roughly.
            var table = Encounters4.TradeDPPt;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i].Length == 0)
                    continue;
                // Nick @ 3, OT @ 7
                if (table[i][7] != pkm.OT_Name)
                    continue;
                lang = i;
                break;
            }
            if (lang == 2) // possible collision with FR/ES/DE. Check nickname
                return pkm.Nickname == table[3][3] ? (int)LanguageID.French : (int)LanguageID.Spanish; // Spanish is same as English

            return lang;
        }

        private static int DetectTradeLanguageG4SurgePikachu(PKM pkm, int lang)
        {
            if (lang == (int)LanguageID.French)
                return (int)LanguageID.English;

            // All have English, regardless of origin version.
            // Detect which language they originated from... roughly.
            var table = Encounters4.TradeHGSS;
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i].Length == 0)
                    continue;
                // Nick @ 6, OT @ 18
                if (table[i][18] != pkm.OT_Name)
                    continue;
                lang = i;
                break;
            }
            if (lang == 2) // possible collision with ES/IT. Check nickname
                return pkm.Nickname == table[4][6] ? (int)LanguageID.Italian : (int)LanguageID.Spanish;

            return lang;
        }

        private void VerifyTrade5(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var EncounterMatch = data.EncounterMatch;
            // Trades for JPN games have language ID of 0, not 1.
            if (pkm.BW)
            {
                int lang = pkm.Language;
                if (pkm.Format == 5 && lang == (int)LanguageID.Japanese)
                    data.AddLine(GetInvalid(string.Format(V5, 0, LanguageID.Japanese), CheckIdentifier.Language));

                lang = Math.Max(lang, 1);
                VerifyTradeTable(data, Encounters5.TradeBW, Encounters5.TradeGift_BW, lang);
            }
            else // B2W2
            {
                if (EncounterMatch is EncounterTrade t && (t.TID == Encounters5.YancyTID || t.TID == Encounters5.CurtisTID))
                    VerifyTradeOTOnly(data, t.TrainerNames);
                else
                    VerifyTradeTable(data, Encounters5.TradeB2W2, Encounters5.TradeGift_B2W2);
            }
        }

        private void VerifyTrade6(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            if (pkm.XY)
                VerifyTradeTable(data, Encounters6.TradeXY, Encounters6.TradeGift_XY, pkm.Language);
            else if (pkm.AO)
                VerifyTradeTable(data, Encounters6.TradeAO, Encounters6.TradeGift_AO, pkm.Language);
        }

        private void VerifyTrade7(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            if (pkm.SM)
                VerifyTradeTable(data, Encounters7.TradeSM, Encounters7.TradeGift_SM, pkm.Language);
            else if (pkm.USUM)
                VerifyTradeTable(data, Encounters7.TradeUSUM, Encounters7.TradeGift_USUM, pkm.Language);
        }

        private void VerifyTrade4Ranch(LegalityAnalysis data) => VerifyTradeOTOnly(data, Encounters4.RanchOTNames);

        private void VerifyTradeTable(LegalityAnalysis data, string[][] ots, EncounterTrade[] table) => VerifyTradeTable(data, ots, table, data.pkm.Language);

        private void VerifyTradeTable(LegalityAnalysis data, string[][] ots, EncounterTrade[] table, int language)
        {
            var validOT = language >= ots.Length ? ots[0] : ots[language];
            var index = Array.IndexOf(table, data.EncounterMatch);
            VerifyTradeOTNick(data, validOT, index);
        }

        private void VerifyTradeOTOnly(LegalityAnalysis data, string[] validOT)
        {
            var result = CheckTradeOTOnly(data, validOT);
            data.AddLine(result);
        }

        private CheckResult CheckTradeOTOnly(LegalityAnalysis data, string[] validOT)
        {
            var pkm = data.pkm;
            if (pkm.IsNicknamed)
                return GetInvalid(V9, CheckIdentifier.Nickname);
            int lang = pkm.Language;
            if (validOT.Length <= lang)
                return GetInvalid(V8, CheckIdentifier.Trainer);
            if (validOT[lang] != pkm.OT_Name)
                return GetInvalid(V10, CheckIdentifier.Trainer);
            return GetValid(V11, CheckIdentifier.Nickname);
        }

        private void VerifyTradeOTNick(LegalityAnalysis data, string[] validOT, int index)
        {
            if (validOT.Length == 0)
            {
                data.AddLine(Get(V7, Severity.Indeterminate, CheckIdentifier.Trainer));
                return;
            }
            if (index == -1 || validOT.Length < index * 2)
            {
                data.AddLine(Get(V8, Severity.Indeterminate, CheckIdentifier.Trainer));
                return;
            }

            string nick = validOT[index];
            string OT = validOT[(validOT.Length / 2) + index];

            var pkm = data.pkm;
            var EncounterMatch = data.EncounterMatch;
            if (!IsNicknameMatch(nick, pkm, EncounterMatch)) // trades that are not nicknamed (but are present in a table with others being named)
                data.AddLine(GetInvalid(V9, CheckIdentifier.Nickname));
            else
                data.AddLine(GetValid(V11, CheckIdentifier.Nickname));

            if (OT != pkm.OT_Name)
                data.AddLine(GetInvalid(V10, CheckIdentifier.Trainer));
        }

        private static bool IsNicknameMatch(string nick, PKM pkm, IEncounterable EncounterMatch)
        {
            if (nick != pkm.Nickname)
                return false;
            if (nick == "Quacklin’" && pkm.Nickname == "Quacklin'")
                return true;
            return ((EncounterTrade)EncounterMatch).IsNicknamed;
        }
    }
}
