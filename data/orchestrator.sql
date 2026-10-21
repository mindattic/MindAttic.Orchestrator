-- MindAttic.Orchestrator database dump (written with every change; the .db file is the store).

-- accounts
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, NULL, NULL, NULL, 1792466400, NULL, 1792533600, NULL, 1792448941.2738085);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, NULL, NULL, NULL, 1792481400, NULL, 1792764000, NULL, 1792463624.0316076);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', 'shampoo1232@mindattic.com', 'MindAttic', 'Team · Max 5x', 19, 1792564200, 59, 1792702800, 1792547865, 1792464053.5616076);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, NULL, NULL, NULL, 1792475400, NULL, 1792929600, NULL, 1792457472.103318);

-- usage_samples
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:13:34Z', 0, '2026-10-18T06:10:00Z', 162425);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:17:38Z', 0, '2026-10-18T06:10:00Z', 283763);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:21:46Z', 0, '2026-10-18T06:10:00Z', 305503);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:25:56Z', 0, '2026-10-18T06:10:00Z', 489681);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:30:09Z', 0, '2026-10-18T06:10:00Z', 518185);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:34:13Z', 1, '2026-10-18T06:10:00Z', 678644);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:38:23Z', 2, '2026-10-18T06:10:00Z', 829131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:42:24Z', 2, '2026-10-18T06:10:00Z', 853831);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:46:38Z', 2, '2026-10-18T06:10:00Z', 910423);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:48:36Z', 3, '2026-10-18T06:10:00Z', 1110634);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:50:36Z', 4, '2026-10-18T06:10:00Z', 1222557);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:54:36Z', 5, '2026-10-18T06:10:00Z', 1491499);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T02:58:45Z', 6, '2026-10-18T06:10:00Z', 1614462);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:02:54Z', 7, '2026-10-18T06:10:00Z', 859846);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:07:01Z', 7, '2026-10-18T06:10:00Z', 873274);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:16:53Z', 7, '2026-10-18T06:10:00Z', 877294);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:20:53Z', 7, '2026-10-18T06:10:00Z', 890082);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:37:28Z', 7, '2026-10-18T06:10:00Z', 893456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:41:35Z', 7, '2026-10-18T06:10:00Z', 955438);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T03:45:43Z', 7, '2026-10-18T06:10:00Z', 960549);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:12:37Z', 8, '2026-10-18T06:10:00Z', 1018506);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:16:45Z', 8, '2026-10-18T06:10:00Z', 1121884);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:22:42Z', 9, '2026-10-18T06:10:00Z', 1336024);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:25:43Z', 10, '2026-10-18T06:10:00Z', 1622481);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:26:12Z', 11, '2026-10-18T06:10:00Z', 1865972);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:26:28Z', 12, '2026-10-18T06:10:00Z', 1994613);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:27:01Z', 13, '2026-10-18T06:10:00Z', 2181332);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:27:21Z', 14, '2026-10-18T06:10:00Z', 2316006);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:27:47Z', 15, '2026-10-18T06:10:00Z', 2456600);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:28:03Z', 16, '2026-10-18T06:10:00Z', 2521508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:29:00Z', 17, '2026-10-18T06:10:00Z', 2592091);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:30:02Z', 18, '2026-10-18T06:10:00Z', 2664246);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:31:00Z', 19, '2026-10-18T06:10:00Z', 2734053);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:32:01Z', 20, '2026-10-18T06:10:00Z', 2785649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:35:28Z', 21, '2026-10-18T06:10:00Z', 2926404);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:37:33Z', 22, '2026-10-18T06:10:00Z', 3161773);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:38:21Z', 23, '2026-10-18T06:10:00Z', 3355133);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:39:59Z', 24, '2026-10-18T06:10:00Z', 3560714);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:42:02Z', 25, '2026-10-18T06:10:00Z', 3697593);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:44:24Z', 26, '2026-10-18T06:10:00Z', 3839848);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:48:27Z', 27, '2026-10-18T06:10:00Z', 3925363);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:52:39Z', 27, '2026-10-18T06:10:00Z', 3969169);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T04:56:45Z', 28, '2026-10-18T06:10:00Z', 3981653);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:00:54Z', 28, '2026-10-18T06:10:00Z', 3999288);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:07:11Z', 29, '2026-10-18T06:10:00Z', 4122611);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:11:12Z', 30, '2026-10-18T06:10:00Z', 4202207);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:14:20Z', 31, '2026-10-18T06:10:00Z', 4304104);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:18:00Z', 32, '2026-10-18T06:10:00Z', 4391459);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:20:26Z', 33, '2026-10-18T06:10:00Z', 4471649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:24:28Z', 34, '2026-10-18T06:10:00Z', 4517312);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:27:24Z', 35, '2026-10-18T06:10:00Z', 4594908);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:31:27Z', 36, '2026-10-18T06:10:00Z', 4647381);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:35:34Z', 36, '2026-10-18T06:10:00Z', 4693599);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:43:25Z', 36, '2026-10-18T06:10:00Z', 4701394);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:47:35Z', 37, '2026-10-18T06:10:00Z', 4742770);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:51:50Z', 37, '2026-10-18T06:10:00Z', 4777136);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:55:34Z', 38, '2026-10-18T06:10:00Z', 4839194);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T05:59:46Z', 39, '2026-10-18T06:10:00Z', 4902256);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:03:57Z', 40, '2026-10-18T06:10:00Z', 4954937);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:08:07Z', 40, '2026-10-18T06:10:00Z', 4976851);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:10:09Z', 0, '2026-10-18T11:10:00Z', 2181);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:10:25Z', 0, '2026-10-18T11:10:00Z', 6239);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:10:44Z', 0, '2026-10-18T11:10:00Z', 11167);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:10:58Z', 0, '2026-10-18T11:10:00Z', 23091);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:11:10Z', 1, '2026-10-18T11:10:00Z', 26608);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:11:40Z', 1, '2026-10-18T11:10:00Z', 36069);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:12:08Z', 1, '2026-10-18T11:10:00Z', 41100);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:12:39Z', 1, '2026-10-18T11:10:00Z', 61631);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:13:51Z', 1, '2026-10-18T11:10:00Z', 79283);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:14:57Z', 1, '2026-10-18T11:10:00Z', 79943);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:15:57Z', 1, '2026-10-18T11:10:00Z', 79943);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:16:44Z', 1, '2026-10-18T11:10:00Z', 103540);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:17:46Z', 1, '2026-10-18T11:10:00Z', 106441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:18:45Z', 1, '2026-10-18T11:10:00Z', 106441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:19:45Z', 1, '2026-10-18T11:10:00Z', 106441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:20:46Z', 1, '2026-10-18T11:10:00Z', 106441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:21:45Z', 1, '2026-10-18T11:10:00Z', 106441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:22:57Z', 1, '2026-10-18T11:10:00Z', 111166);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:23:36Z', 1, '2026-10-18T11:10:00Z', 111166);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:23:46Z', 2, '2026-10-18T11:10:00Z', 113361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:24:37Z', 2, '2026-10-18T11:10:00Z', 126205);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:28:48Z', 3, '2026-10-18T11:10:00Z', 194929);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:32:50Z', 3, '2026-10-18T11:10:00Z', 297522);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:33:50Z', 4, '2026-10-18T11:10:00Z', 344751);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:37:59Z', 4, '2026-10-18T11:10:00Z', 363782);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:41:36Z', 5, '2026-10-18T11:10:00Z', 392561);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:45:42Z', 6, '2026-10-18T11:10:00Z', 420550);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:47:38Z', 7, '2026-10-18T11:10:00Z', 488167);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:49:53Z', 8, '2026-10-18T11:10:00Z', 559861);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:53:54Z', 9, '2026-10-18T11:10:00Z', 636827);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T06:58:05Z', 9, '2026-10-18T11:10:00Z', 658364);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:01:36Z', 11, '2026-10-18T11:10:00Z', 1158593);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:05:44Z', 12, '2026-10-18T11:10:00Z', 1201926);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:09:51Z', 13, '2026-10-18T11:10:00Z', 1237311);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:13:54Z', 14, '2026-10-18T11:10:00Z', 1307098);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:18:06Z', 14, '2026-10-18T11:10:00Z', 1354010);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:22:13Z', 15, '2026-10-18T11:10:00Z', 1418930);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:26:18Z', 16, '2026-10-18T11:10:00Z', 1459812);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:30:26Z', 16, '2026-10-18T11:10:00Z', 1553508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:31:34Z', 17, '2026-10-18T11:10:00Z', 1592087);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:34:19Z', 18, '2026-10-18T11:10:00Z', 1673945);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:37:21Z', 19, '2026-10-18T11:10:00Z', 1765787);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:41:25Z', 20, '2026-10-18T11:10:00Z', 1839060);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:45:31Z', 21, '2026-10-18T11:10:00Z', 1913703);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:49:34Z', 22, '2026-10-18T11:10:00Z', 1979828);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:51:25Z', 23, '2026-10-18T11:10:00Z', 2098597);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:55:30Z', 24, '2026-10-18T11:10:00Z', 2127970);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T07:59:43Z', 25, '2026-10-18T11:10:00Z', 2204313);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:03:50Z', 25, '2026-10-18T11:10:00Z', 2257716);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:07:53Z', 26, '2026-10-18T11:10:00Z', 2354629);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:11:56Z', 27, '2026-10-18T11:10:00Z', 2577441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:14:01Z', 28, '2026-10-18T11:10:00Z', 2829631);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:18:03Z', 29, '2026-10-18T11:10:00Z', 2916057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:22:14Z', 29, '2026-10-18T11:10:00Z', 2965161);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:25:11Z', 30, '2026-10-18T11:10:00Z', 3065023);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T08:29:12Z', 31, '2026-10-18T11:10:00Z', 3102244);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:28:56Z', 0, '2026-10-18T20:20:00Z', 267906);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:29:05Z', 1, '2026-10-18T20:20:00Z', 277170);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:33:08Z', 2, '2026-10-18T20:20:00Z', 313919);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:37:22Z', 2, '2026-10-18T20:20:00Z', 330775);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:39:16Z', 4, '2026-10-18T20:20:00Z', 1029637);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:43:20Z', 6, '2026-10-18T20:20:00Z', 1321414);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:47:23Z', 6, '2026-10-18T20:20:00Z', 1334222);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:51:24Z', 6, '2026-10-18T20:20:00Z', 1352490);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:55:46Z', 6, '2026-10-18T20:20:00Z', 1361666);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T15:57:45Z', 7, '2026-10-18T20:20:00Z', 1426825);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:00:07Z', 8, '2026-10-18T20:20:00Z', 1498648);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:03:48Z', 9, '2026-10-18T20:20:00Z', 1565695);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:08:00Z', 10, '2026-10-18T20:20:00Z', 1588664);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:12:01Z', 10, '2026-10-18T20:20:00Z', 1627703);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:13:31Z', 11, '2026-10-18T20:20:00Z', 1669933);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:17:32Z', 12, '2026-10-18T20:20:00Z', 1700348);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:20:20Z', 13, '2026-10-18T20:20:00Z', 1744239);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:24:25Z', 14, '2026-10-18T20:20:00Z', 1784133);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:26:17Z', 15, '2026-10-18T20:20:00Z', 3054071);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:29:19Z', 16, '2026-10-18T20:20:00Z', 3461170);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:32:46Z', 17, '2026-10-18T20:20:00Z', 3612818);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:35:46Z', 18, '2026-10-18T20:20:00Z', 3698121);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:38:02Z', 19, '2026-10-18T20:20:00Z', 3767941);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:42:16Z', 20, '2026-10-18T20:20:00Z', 3803795);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:45:56Z', 21, '2026-10-18T20:20:00Z', 3838012);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:50:00Z', 22, '2026-10-18T20:20:00Z', 3866979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:54:11Z', 22, '2026-10-18T20:20:00Z', 3881947);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T16:58:26Z', 23, '2026-10-18T20:20:00Z', 3918474);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:02:33Z', 24, '2026-10-18T20:20:00Z', 3955491);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:06:40Z', 25, '2026-10-18T20:20:00Z', 3996680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:10:21Z', 26, '2026-10-18T20:20:00Z', 4272415);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:11:53Z', 27, '2026-10-18T20:20:00Z', 4456303);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:13:55Z', 28, '2026-10-18T20:20:00Z', 4597407);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:16:07Z', 29, '2026-10-18T20:20:00Z', 4728373);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:18:00Z', 30, '2026-10-18T20:20:00Z', 4848298);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:20:11Z', 31, '2026-10-18T20:20:00Z', 4955401);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:21:00Z', 32, '2026-10-18T20:20:00Z', 5124645);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:21:47Z', 33, '2026-10-18T20:20:00Z', 5255989);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:22:36Z', 34, '2026-10-18T20:20:00Z', 5419488);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:23:36Z', 35, '2026-10-18T20:20:00Z', 5552988);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:25:38Z', 36, '2026-10-18T20:20:00Z', 5663701);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:26:56Z', 37, '2026-10-18T20:20:00Z', 5744443);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:27:54Z', 38, '2026-10-18T20:20:00Z', 5961586);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:28:52Z', 39, '2026-10-18T20:20:00Z', 6110790);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:29:38Z', 40, '2026-10-18T20:20:00Z', 6277443);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:30:37Z', 41, '2026-10-18T20:20:00Z', 6361412);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:32:05Z', 42, '2026-10-18T20:20:00Z', 6468203);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:33:30Z', 44, '2026-10-18T20:20:00Z', 7126512);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:34:49Z', 45, '2026-10-18T20:20:00Z', 7182450);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:37:16Z', 46, '2026-10-18T20:20:00Z', 7317812);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:39:35Z', 47, '2026-10-18T20:20:00Z', 7625327);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:39:47Z', 48, '2026-10-18T20:20:00Z', 8010499);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:41:31Z', 49, '2026-10-18T20:20:00Z', 8302838);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:42:02Z', 50, '2026-10-18T20:20:00Z', 8312166);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:44:29Z', 51, '2026-10-18T20:20:00Z', 8359659);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:46:38Z', 52, '2026-10-18T20:20:00Z', 8857454);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:47:49Z', 53, '2026-10-18T20:20:00Z', 8870158);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:49:00Z', 54, '2026-10-18T20:20:00Z', 9154719);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:53:08Z', 55, '2026-10-18T20:20:00Z', 9396538);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T17:57:24Z', 56, '2026-10-18T20:20:00Z', 9424734);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:01:28Z', 56, '2026-10-18T20:20:00Z', 9631619);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:02:25Z', 57, '2026-10-18T20:20:00Z', 9909214);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:06:23Z', 58, '2026-10-18T20:20:00Z', 10055118);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:10:29Z', 59, '2026-10-18T20:20:00Z', 10189935);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:14:40Z', 59, '2026-10-18T20:20:00Z', 10290881);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:18:54Z', 60, '2026-10-18T20:20:00Z', 10376453);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:22:57Z', 61, '2026-10-18T20:20:00Z', 10642419);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:27:05Z', 62, '2026-10-18T20:20:00Z', 10786892);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:30:39Z', 63, '2026-10-18T20:20:00Z', 11008586);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:34:40Z', 64, '2026-10-18T20:20:00Z', 11109142);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:36:39Z', 65, '2026-10-18T20:20:00Z', 11396534);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:40:38Z', 66, '2026-10-18T20:20:00Z', 11520425);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:42:46Z', 67, '2026-10-18T20:20:00Z', 11669977);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:43:36Z', 68, '2026-10-18T20:20:00Z', 11904233);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:45:17Z', 69, '2026-10-18T20:20:00Z', 12005171);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:48:39Z', 70, '2026-10-18T20:20:00Z', 12108906);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:50:18Z', 71, '2026-10-18T20:20:00Z', 12347404);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:54:33Z', 71, '2026-10-18T20:20:00Z', 12370026);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:57:58Z', 72, '2026-10-18T20:20:00Z', 12488816);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:58:39Z', 73, '2026-10-18T20:20:00Z', 12736248);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T18:59:25Z', 74, '2026-10-18T20:20:00Z', 12890912);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:00:25Z', 76, '2026-10-18T20:20:00Z', 13173117);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:01:02Z', 77, '2026-10-18T20:20:00Z', 13283804);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:03:39Z', 78, '2026-10-18T20:20:00Z', 13369556);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:07:40Z', 79, '2026-10-18T20:20:00Z', 13417569);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:11:48Z', 80, '2026-10-18T20:20:00Z', 13484018);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:15:56Z', 81, '2026-10-18T20:20:00Z', 13533236);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:20:06Z', 82, '2026-10-18T20:20:00Z', 13753092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:24:10Z', 82, '2026-10-18T20:20:00Z', 13810535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:26:39Z', 83, '2026-10-18T20:20:00Z', 13966576);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:30:46Z', 84, '2026-10-18T20:20:00Z', 14039535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:33:24Z', 85, '2026-10-18T20:20:00Z', 14214246);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:37:28Z', 86, '2026-10-18T20:20:00Z', 14243182);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:41:45Z', 86, '2026-10-18T20:20:00Z', 14393834);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:45:49Z', 87, '2026-10-18T20:20:00Z', 14405947);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:51:23Z', 87, '2026-10-18T20:20:00Z', 14565757);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T19:56:35Z', 87, '2026-10-18T20:20:00Z', 14566945);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:00:46Z', 87, '2026-10-18T20:20:00Z', 14574222);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:09:44Z', 88, '2026-10-18T20:20:00Z', 14827091);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:13:45Z', 88, '2026-10-18T20:20:00Z', 14948267);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:17:46Z', 89, '2026-10-18T20:20:00Z', 14957323);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:20:48Z', 0, '2026-10-19T01:20:00Z', 1312);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:21:11Z', 0, '2026-10-19T01:20:00Z', 3155);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:21:21Z', 0, '2026-10-19T01:20:00Z', 52175);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:21:35Z', 1, '2026-10-19T01:20:00Z', 96636);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:21:56Z', 1, '2026-10-19T01:20:00Z', 165469);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:22:19Z', 2, '2026-10-19T01:20:00Z', 275824);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:22:56Z', 2, '2026-10-19T01:20:00Z', 371635);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:23:04Z', 3, '2026-10-19T01:20:00Z', 392841);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:23:44Z', 4, '2026-10-19T01:20:00Z', 492635);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:24:08Z', 4, '2026-10-19T01:20:00Z', 552234);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:24:43Z', 5, '2026-10-19T01:20:00Z', 621556);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:25:03Z', 5, '2026-10-19T01:20:00Z', 636708);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:25:30Z', 6, '2026-10-19T01:20:00Z', 696063);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:26:01Z', 6, '2026-10-19T01:20:00Z', 747204);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:26:35Z', 7, '2026-10-19T01:20:00Z', 853976);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:27:07Z', 7, '2026-10-19T01:20:00Z', 903874);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:27:39Z', 8, '2026-10-19T01:20:00Z', 960650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:28:00Z', 8, '2026-10-19T01:20:00Z', 983700);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:28:59Z', 8, '2026-10-19T01:20:00Z', 998907);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:29:08Z', 9, '2026-10-19T01:20:00Z', 1008047);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:29:57Z', 9, '2026-10-19T01:20:00Z', 1020507);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:30:35Z', 9, '2026-10-19T01:20:00Z', 1024943);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:32:18Z', 10, '2026-10-19T01:20:00Z', 1150655);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:34:39Z', 11, '2026-10-19T01:20:00Z', 1513607);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:35:39Z', 12, '2026-10-19T01:20:00Z', 1612829);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:36:39Z', 14, '2026-10-19T01:20:00Z', 2030227);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:37:58Z', 15, '2026-10-19T01:20:00Z', 2122102);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:39:39Z', 16, '2026-10-19T01:20:00Z', 2198025);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:42:39Z', 17, '2026-10-19T01:20:00Z', 2254225);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:45:21Z', 18, '2026-10-19T01:20:00Z', 2311979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:48:26Z', 19, '2026-10-19T01:20:00Z', 2635825);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:50:13Z', 20, '2026-10-19T01:20:00Z', 2664952);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:50:28Z', 21, '2026-10-19T01:20:00Z', 3123729);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:54:38Z', 22, '2026-10-19T01:20:00Z', 3128549);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T20:57:37Z', 25, '2026-10-19T01:20:00Z', 3939387);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:00:38Z', 26, '2026-10-19T01:20:00Z', 4029506);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:02:01Z', 27, '2026-10-19T01:20:00Z', 4319161);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:06:04Z', 28, '2026-10-19T01:20:00Z', 4421154);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:10:13Z', 28, '2026-10-19T01:20:00Z', 4425550);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:14:27Z', 30, '2026-10-19T01:20:00Z', 4552435);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:18:29Z', 32, '2026-10-19T01:20:00Z', 5004663);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:22:37Z', 34, '2026-10-19T01:20:00Z', 5300347);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:28:12Z', 34, '2026-10-19T01:20:00Z', 5307094);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:32:14Z', 35, '2026-10-19T01:20:00Z', 5347621);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T21:50:03Z', 35, '2026-10-19T01:20:00Z', 5350110);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T22:55:23Z', 35, '2026-10-19T01:20:00Z', 5410013);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:00:42Z', 35, '2026-10-19T01:20:00Z', 5413310);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:04:44Z', 35, '2026-10-19T01:20:00Z', 5422931);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:08:59Z', 35, '2026-10-19T01:20:00Z', 5442751);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:13:10Z', 35, '2026-10-19T01:20:00Z', 5446600);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:17:25Z', 35, '2026-10-19T01:20:00Z', 5452665);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:21:32Z', 36, '2026-10-19T01:20:00Z', 5459800);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-18T23:25:37Z', 36, '2026-10-19T01:20:00Z', 5491350);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:34:25Z', 36, '2026-10-19T01:20:00Z', 5556321);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:37:19Z', 37, '2026-10-19T01:20:00Z', 5872653);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:37:57Z', 38, '2026-10-19T01:20:00Z', 6006806);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:38:56Z', 39, '2026-10-19T01:20:00Z', 6159696);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:40:05Z', 40, '2026-10-19T01:20:00Z', 6314725);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:41:19Z', 41, '2026-10-19T01:20:00Z', 6450958);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:42:13Z', 42, '2026-10-19T01:20:00Z', 6603099);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:43:15Z', 43, '2026-10-19T01:20:00Z', 6787002);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:44:54Z', 44, '2026-10-19T01:20:00Z', 6884068);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:46:55Z', 45, '2026-10-19T01:20:00Z', 7028005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:48:50Z', 46, '2026-10-19T01:20:00Z', 7156159);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:51:53Z', 47, '2026-10-19T01:20:00Z', 7251147);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:53:02Z', 48, '2026-10-19T01:20:00Z', 7387197);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:54:28Z', 49, '2026-10-19T01:20:00Z', 7610328);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:55:42Z', 50, '2026-10-19T01:20:00Z', 7718087);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:57:23Z', 51, '2026-10-19T01:20:00Z', 7794285);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T00:59:08Z', 52, '2026-10-19T01:20:00Z', 7958011);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:01:02Z', 53, '2026-10-19T01:20:00Z', 8077511);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:02:19Z', 54, '2026-10-19T01:20:00Z', 8246626);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:04:02Z', 55, '2026-10-19T01:20:00Z', 8383443);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:05:01Z', 56, '2026-10-19T01:20:00Z', 8648161);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:05:41Z', 57, '2026-10-19T01:20:00Z', 8818174);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:07:01Z', 58, '2026-10-19T01:20:00Z', 8927036);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:09:56Z', 59, '2026-10-19T01:20:00Z', 9026862);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:11:17Z', 60, '2026-10-19T01:20:00Z', 9166846);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:12:45Z', 61, '2026-10-19T01:20:00Z', 9256048);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:13:58Z', 62, '2026-10-19T01:20:00Z', 9404171);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:16:02Z', 63, '2026-10-19T01:20:00Z', 9514359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:17:22Z', 64, '2026-10-19T01:20:00Z', 9616945);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:19:41Z', 65, '2026-10-19T01:20:00Z', 9803953);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:20:21Z', 0, '2026-10-19T06:20:00Z', 123639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:20:33Z', 1, '2026-10-19T06:20:00Z', 160472);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:20:47Z', 1, '2026-10-19T06:20:00Z', 220308);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:21:05Z', 2, '2026-10-19T06:20:00Z', 384155);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:21:22Z', 2, '2026-10-19T06:20:00Z', 471027);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:21:53Z', 3, '2026-10-19T06:20:00Z', 564285);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:22:30Z', 4, '2026-10-19T06:20:00Z', 674742);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:23:02Z', 4, '2026-10-19T06:20:00Z', 742468);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:23:27Z', 5, '2026-10-19T06:20:00Z', 797681);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:23:44Z', 5, '2026-10-19T06:20:00Z', 843684);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:24:13Z', 6, '2026-10-19T06:20:00Z', 947736);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:24:53Z', 6, '2026-10-19T06:20:00Z', 1053477);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:25:05Z', 7, '2026-10-19T06:20:00Z', 1082958);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:25:53Z', 7, '2026-10-19T06:20:00Z', 1116492);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:26:16Z', 8, '2026-10-19T06:20:00Z', 1147361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:26:49Z', 8, '2026-10-19T06:20:00Z', 1168237);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:27:03Z', 9, '2026-10-19T06:20:00Z', 1345610);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:28:36Z', 10, '2026-10-19T06:20:00Z', 1446359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:30:27Z', 11, '2026-10-19T06:20:00Z', 1524064);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:32:02Z', 12, '2026-10-19T06:20:00Z', 1600817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:33:39Z', 13, '2026-10-19T06:20:00Z', 1683330);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:35:03Z', 14, '2026-10-19T06:20:00Z', 1761762);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:36:49Z', 15, '2026-10-19T06:20:00Z', 1837517);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:38:07Z', 16, '2026-10-19T06:20:00Z', 1937177);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:40:03Z', 17, '2026-10-19T06:20:00Z', 2017494);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:41:16Z', 18, '2026-10-19T06:20:00Z', 2073365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:42:55Z', 19, '2026-10-19T06:20:00Z', 2163730);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:44:44Z', 20, '2026-10-19T06:20:00Z', 2246520);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:46:46Z', 21, '2026-10-19T06:20:00Z', 2535029);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:47:39Z', 22, '2026-10-19T06:20:00Z', 2572630);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:51:03Z', 23, '2026-10-19T06:20:00Z', 2739809);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:55:18Z', 24, '2026-10-19T06:20:00Z', 2895496);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T01:59:23Z', 25, '2026-10-19T06:20:00Z', 2977954);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:02:23Z', 26, '2026-10-19T06:20:00Z', 3121076);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:04:36Z', 27, '2026-10-19T06:20:00Z', 3248921);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:05:49Z', 28, '2026-10-19T06:20:00Z', 3439184);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:07:20Z', 29, '2026-10-19T06:20:00Z', 3572849);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:08:53Z', 30, '2026-10-19T06:20:00Z', 3686733);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:11:23Z', 31, '2026-10-19T06:20:00Z', 3806736);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:15:42Z', 32, '2026-10-19T06:20:00Z', 3870902);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:18:26Z', 33, '2026-10-19T06:20:00Z', 4032618);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:19:12Z', 34, '2026-10-19T06:20:00Z', 4244353);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:20:46Z', 35, '2026-10-19T06:20:00Z', 4362237);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:24:56Z', 36, '2026-10-19T06:20:00Z', 4481423);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:28:59Z', 37, '2026-10-19T06:20:00Z', 4576808);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:31:23Z', 38, '2026-10-19T06:20:00Z', 4702952);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:33:40Z', 39, '2026-10-19T06:20:00Z', 4864963);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:36:48Z', 40, '2026-10-19T06:20:00Z', 5025071);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:40:24Z', 41, '2026-10-19T06:20:00Z', 5136979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:42:41Z', 42, '2026-10-19T06:20:00Z', 5224214);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:45:02Z', 43, '2026-10-19T06:20:00Z', 5358865);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:47:42Z', 44, '2026-10-19T06:20:00Z', 5474759);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:48:46Z', 45, '2026-10-19T06:20:00Z', 5706135);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:50:19Z', 46, '2026-10-19T06:20:00Z', 5827678);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:53:37Z', 47, '2026-10-19T06:20:00Z', 5943068);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:56:02Z', 48, '2026-10-19T06:20:00Z', 6060465);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T02:58:46Z', 49, '2026-10-19T06:20:00Z', 6140111);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:01:26Z', 50, '2026-10-19T06:20:00Z', 6261920);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:03:13Z', 51, '2026-10-19T06:20:00Z', 6360969);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:05:25Z', 52, '2026-10-19T06:20:00Z', 6462676);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:07:30Z', 53, '2026-10-19T06:20:00Z', 6557367);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:11:46Z', 54, '2026-10-19T06:20:00Z', 6619961);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:15:37Z', 55, '2026-10-19T06:20:00Z', 6887512);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:17:37Z', 56, '2026-10-19T06:20:00Z', 7018155);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:20:41Z', 57, '2026-10-19T06:20:00Z', 7126883);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:21:41Z', 58, '2026-10-19T06:20:00Z', 7403723);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:24:41Z', 59, '2026-10-19T06:20:00Z', 7470292);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:28:57Z', 60, '2026-10-19T06:20:00Z', 7532804);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:32:58Z', 61, '2026-10-19T06:20:00Z', 7615260);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:37:14Z', 62, '2026-10-19T06:20:00Z', 7669976);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:41:29Z', 63, '2026-10-19T06:20:00Z', 7731319);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:47:26Z', 64, '2026-10-19T06:20:00Z', 7933105);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:51:35Z', 65, '2026-10-19T06:20:00Z', 7984671);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:55:41Z', 66, '2026-10-19T06:20:00Z', 8124080);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T03:58:01Z', 67, '2026-10-19T06:20:00Z', 8272505);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:00:03Z', 68, '2026-10-19T06:20:00Z', 8316183);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:04:09Z', 69, '2026-10-19T06:20:00Z', 8393782);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:06:50Z', 70, '2026-10-19T06:20:00Z', 8467272);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:09:09Z', 71, '2026-10-19T06:20:00Z', 8522061);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:12:35Z', 72, '2026-10-19T06:20:00Z', 8570371);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:16:38Z', 73, '2026-10-19T06:20:00Z', 8661520);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:19:18Z', 74, '2026-10-19T06:20:00Z', 8736416);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:22:19Z', 75, '2026-10-19T06:20:00Z', 9062233);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:25:35Z', 76, '2026-10-19T06:20:00Z', 9093891);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:29:37Z', 76, '2026-10-19T06:20:00Z', 9114731);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:33:35Z', 77, '2026-10-19T06:20:00Z', 9214965);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:37:45Z', 78, '2026-10-19T06:20:00Z', 9301907);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:41:56Z', 79, '2026-10-19T06:20:00Z', 9338579);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:44:52Z', 80, '2026-10-19T06:20:00Z', 9619719);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:49:00Z', 80, '2026-10-19T06:20:00Z', 9620926);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:53:16Z', 80, '2026-10-19T06:20:00Z', 9636111);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T04:57:17Z', 81, '2026-10-19T06:20:00Z', 9755575);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:01:26Z', 82, '2026-10-19T06:20:00Z', 9816298);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:05:30Z', 82, '2026-10-19T06:20:00Z', 9852802);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:07:41Z', 84, '2026-10-19T06:20:00Z', 10417985);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:12:28Z', 84, '2026-10-19T06:20:00Z', 10419936);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:17:36Z', 84, '2026-10-19T06:20:00Z', 10426328);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:19:41Z', 85, '2026-10-19T06:20:00Z', 10478944);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:23:45Z', 86, '2026-10-19T06:20:00Z', 10507461);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:27:46Z', 86, '2026-10-19T06:20:00Z', 10508495);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:31:41Z', 87, '2026-10-19T06:20:00Z', 10573033);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:35:43Z', 87, '2026-10-19T06:20:00Z', 10577510);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:40:00Z', 88, '2026-10-19T06:20:00Z', 10630493);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:43:41Z', 89, '2026-10-19T06:20:00Z', 11316908);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:49:03Z', 90, '2026-10-19T06:20:00Z', 11320369);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:53:36Z', 90, '2026-10-19T06:20:00Z', 11323753);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T05:57:44Z', 91, '2026-10-19T06:20:00Z', 11336946);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:01:59Z', 91, '2026-10-19T06:20:00Z', 11340927);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:06:41Z', 92, '2026-10-19T06:20:00Z', 12042902);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:11:22Z', 93, '2026-10-19T06:20:00Z', 12044054);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:15:25Z', 95, '2026-10-19T06:20:00Z', 12619093);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:21:09Z', 0, '2026-10-19T11:20:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:22:22Z', 0, '2026-10-19T11:20:00Z', 2752);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:22:47Z', 0, '2026-10-19T11:20:00Z', 6109);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:23:09Z', 0, '2026-10-19T11:20:00Z', 9454);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:24:06Z', 0, '2026-10-19T11:20:00Z', 19889);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:29:22Z', 1, '2026-10-19T11:20:00Z', 23423);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:29:47Z', 1, '2026-10-19T11:20:00Z', 25763);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T06:29:58Z', 1, '2026-10-19T11:20:00Z', 27843);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:05:04Z', 6, '2026-10-19T11:20:00Z', 855088);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:12:42Z', 10, '2026-10-19T11:20:00Z', 1286682);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:13:41Z', 10, '2026-10-19T11:20:00Z', 1320600);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:15:42Z', 11, '2026-10-19T11:20:00Z', 1412874);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:34:48Z', 15, '2026-10-19T11:20:00Z', 2381177);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:35:09Z', 15, '2026-10-19T11:20:00Z', 2385884);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:35:17Z', 15, '2026-10-19T11:20:00Z', 2391757);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:35:55Z', 16, '2026-10-19T11:20:00Z', 2539539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:36:12Z', 17, '2026-10-19T11:20:00Z', 2662604);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:37:42Z', 18, '2026-10-19T11:20:00Z', 2904290);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T07:52:44Z', 27, '2026-10-19T11:20:00Z', 4030365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:04:07Z', 33, '2026-10-19T11:20:00Z', 4722737);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:07:38Z', 35, '2026-10-19T11:20:00Z', 5266590);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:12:42Z', 36, '2026-10-19T11:20:00Z', 5305256);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:17:22Z', 37, '2026-10-19T11:20:00Z', 5568924);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:17:58Z', 38, '2026-10-19T11:20:00Z', 5587438);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:18:36Z', 38, '2026-10-19T11:20:00Z', 5683609);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:20:42Z', 39, '2026-10-19T11:20:00Z', 5716785);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:26:26Z', 42, '2026-10-19T11:20:00Z', 6188166);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:38:43Z', 44, '2026-10-19T11:20:00Z', 6805549);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:39:42Z', 44, '2026-10-19T11:20:00Z', 6805549);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:42:29Z', 46, '2026-10-19T11:20:00Z', 7269748);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:46:42Z', 46, '2026-10-19T11:20:00Z', 7436248);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T08:50:16Z', 47, '2026-10-19T11:20:00Z', 7629152);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T09:55:20Z', 47, '2026-10-19T11:20:00Z', 7629664);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:41:02Z', 49, '2026-10-19T11:20:00Z', 8192677);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:41:20Z', 49, '2026-10-19T11:20:00Z', 8192677);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:42:01Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:43:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:44:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:45:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:46:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:47:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:48:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:49:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:50:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:51:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:52:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:53:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:54:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:55:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:56:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:57:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:58:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T10:59:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:00:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:01:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:02:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:03:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:04:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:05:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:06:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:07:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:08:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:09:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:10:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:11:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:12:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:13:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:14:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:15:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:16:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:17:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:18:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T11:19:02Z', 49, '2026-10-19T11:20:00Z', 8210693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T14:35:55Z', 0, '2026-10-19T19:30:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T14:42:21Z', 2, '2026-10-19T19:30:00Z', 648092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T14:43:04Z', 3, '2026-10-19T19:30:00Z', 737606);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T14:44:41Z', 3, '2026-10-19T19:30:00Z', 832875);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T14:48:41Z', 4, '2026-10-19T19:30:00Z', 897468);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T14:59:42Z', 16, '2026-10-19T19:30:00Z', 2772937);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:05:16Z', 18, '2026-10-19T19:30:00Z', 2983312);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:10:07Z', 18, '2026-10-19T19:30:00Z', 3112572);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:10:22Z', 18, '2026-10-19T19:30:00Z', 3118766);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:10:28Z', 19, '2026-10-19T19:30:00Z', 3238036);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:10:45Z', 19, '2026-10-19T19:30:00Z', 3242769);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:11:06Z', 19, '2026-10-19T19:30:00Z', 3250155);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:11:41Z', 19, '2026-10-19T19:30:00Z', 3311526);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:12:41Z', 19, '2026-10-19T19:30:00Z', 3321749);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:13:41Z', 19, '2026-10-19T19:30:00Z', 3327694);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:13:55Z', 19, '2026-10-19T19:30:00Z', 3327694);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:14:10Z', 19, '2026-10-19T19:30:00Z', 3388410);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:14:15Z', 20, '2026-10-19T19:30:00Z', 3396546);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:14:36Z', 20, '2026-10-19T19:30:00Z', 3420984);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:15:54Z', 22, '2026-10-19T19:30:00Z', 3834715);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:16:06Z', 22, '2026-10-19T19:30:00Z', 4408259);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:16:18Z', 23, '2026-10-19T19:30:00Z', 4422481);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:16:36Z', 24, '2026-10-19T19:30:00Z', 4444101);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:17:15Z', 24, '2026-10-19T19:30:00Z', 4473326);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:17:54Z', 24, '2026-10-19T19:30:00Z', 4498429);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:18:53Z', 24, '2026-10-19T19:30:00Z', 4519650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:19:53Z', 25, '2026-10-19T19:30:00Z', 4529117);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:20:23Z', 25, '2026-10-19T19:30:00Z', 4540886);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:20:41Z', 25, '2026-10-19T19:30:00Z', 4542700);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:21:20Z', 25, '2026-10-19T19:30:00Z', 4557446);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:21:48Z', 26, '2026-10-19T19:30:00Z', 4713057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:22:16Z', 26, '2026-10-19T19:30:00Z', 4726760);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:22:35Z', 26, '2026-10-19T19:30:00Z', 4749525);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:22:51Z', 28, '2026-10-19T19:30:00Z', 4992413);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:25:20Z', 29, '2026-10-19T19:30:00Z', 5312864);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:27:41Z', 30, '2026-10-19T19:30:00Z', 5426797);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:29:20Z', 31, '2026-10-19T19:30:00Z', 5560967);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:31:53Z', 32, '2026-10-19T19:30:00Z', 5646189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:33:39Z', 33, '2026-10-19T19:30:00Z', 5812078);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:34:46Z', 34, '2026-10-19T19:30:00Z', 6036324);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:36:15Z', 35, '2026-10-19T19:30:00Z', 6163551);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:38:08Z', 36, '2026-10-19T19:30:00Z', 6292688);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:40:09Z', 37, '2026-10-19T19:30:00Z', 6388983);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:44:08Z', 38, '2026-10-19T19:30:00Z', 6599134);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:44:44Z', 39, '2026-10-19T19:30:00Z', 6670769);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:46:20Z', 40, '2026-10-19T19:30:00Z', 6793405);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:47:49Z', 41, '2026-10-19T19:30:00Z', 7028765);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:48:35Z', 42, '2026-10-19T19:30:00Z', 7081947);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:49:40Z', 43, '2026-10-19T19:30:00Z', 7224088);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:52:08Z', 44, '2026-10-19T19:30:00Z', 7388198);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:53:49Z', 45, '2026-10-19T19:30:00Z', 7469326);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T15:58:05Z', 46, '2026-10-19T19:30:00Z', 7520032);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:00:10Z', 47, '2026-10-19T19:30:00Z', 7628081);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:01:10Z', 48, '2026-10-19T19:30:00Z', 7797210);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:02:58Z', 49, '2026-10-19T19:30:00Z', 7987933);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:05:37Z', 50, '2026-10-19T19:30:00Z', 8199974);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:08:25Z', 51, '2026-10-19T19:30:00Z', 8284039);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:10:13Z', 52, '2026-10-19T19:30:00Z', 8359973);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:13:28Z', 53, '2026-10-19T19:30:00Z', 8569067);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:15:50Z', 54, '2026-10-19T19:30:00Z', 8665439);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:17:50Z', 55, '2026-10-19T19:30:00Z', 8748470);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:20:16Z', 56, '2026-10-19T19:30:00Z', 8868967);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:22:44Z', 57, '2026-10-19T19:30:00Z', 8993386);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:26:46Z', 58, '2026-10-19T19:30:00Z', 9062439);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:29:40Z', 59, '2026-10-19T19:30:00Z', 9270710);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:33:43Z', 60, '2026-10-19T19:30:00Z', 9364956);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:36:02Z', 61, '2026-10-19T19:30:00Z', 9458771);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:38:50Z', 62, '2026-10-19T19:30:00Z', 9537637);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:40:50Z', 63, '2026-10-19T19:30:00Z', 9753287);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:44:08Z', 64, '2026-10-19T19:30:00Z', 9813630);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:48:14Z', 65, '2026-10-19T19:30:00Z', 9841348);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:50:41Z', 66, '2026-10-19T19:30:00Z', 10177061);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:54:48Z', 67, '2026-10-19T19:30:00Z', 10218468);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T16:58:49Z', 67, '2026-10-19T19:30:00Z', 10242891);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:02:06Z', 68, '2026-10-19T19:30:00Z', 10307398);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:06:22Z', 69, '2026-10-19T19:30:00Z', 10366260);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:11:51Z', 70, '2026-10-19T19:30:00Z', 10487709);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:16:00Z', 71, '2026-10-19T19:30:00Z', 10570877);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:20:16Z', 72, '2026-10-19T19:30:00Z', 10625438);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:24:21Z', 72, '2026-10-19T19:30:00Z', 10686660);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:28:25Z', 72, '2026-10-19T19:30:00Z', 10701259);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:30:28Z', 73, '2026-10-19T19:30:00Z', 10754889);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:31:50Z', 74, '2026-10-19T19:30:00Z', 10945401);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:35:50Z', 75, '2026-10-19T19:30:00Z', 11062507);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:40:07Z', 76, '2026-10-19T19:30:00Z', 11125341);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:44:08Z', 76, '2026-10-19T19:30:00Z', 11147661);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:46:50Z', 77, '2026-10-19T19:30:00Z', 11224530);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:48:39Z', 78, '2026-10-19T19:30:00Z', 11339475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:51:02Z', 79, '2026-10-19T19:30:00Z', 11417937);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:53:43Z', 80, '2026-10-19T19:30:00Z', 11606967);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T17:57:51Z', 81, '2026-10-19T19:30:00Z', 11732366);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:02:03Z', 81, '2026-10-19T19:30:00Z', 11765653);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:06:10Z', 82, '2026-10-19T19:30:00Z', 11809963);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:09:19Z', 83, '2026-10-19T19:30:00Z', 11879410);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:12:26Z', 84, '2026-10-19T19:30:00Z', 11935372);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:16:40Z', 85, '2026-10-19T19:30:00Z', 12071143);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:20:54Z', 86, '2026-10-19T19:30:00Z', 12189242);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:25:03Z', 86, '2026-10-19T19:30:00Z', 12314957);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:27:35Z', 87, '2026-10-19T19:30:00Z', 12333103);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:31:41Z', 88, '2026-10-19T19:30:00Z', 12466436);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:35:56Z', 88, '2026-10-19T19:30:00Z', 12486310);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:40:06Z', 89, '2026-10-19T19:30:00Z', 12608167);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:44:18Z', 89, '2026-10-19T19:30:00Z', 12610472);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:48:22Z', 89, '2026-10-19T19:30:00Z', 12657566);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:50:18Z', 90, '2026-10-19T19:30:00Z', 12675110);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:51:50Z', 91, '2026-10-19T19:30:00Z', 12809652);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T18:56:04Z', 92, '2026-10-19T19:30:00Z', 12830030);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:03:49Z', 93, '2026-10-19T19:30:00Z', 13326569);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:04:49Z', 94, '2026-10-19T19:30:00Z', 13326569);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:06:39Z', 95, '2026-10-19T19:30:00Z', 13330335);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:10:52Z', 95, '2026-10-19T19:30:00Z', 13418462);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:15:07Z', 95, '2026-10-19T19:30:00Z', 13530289);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:19:08Z', 96, '2026-10-19T19:30:00Z', 13533519);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:23:19Z', 96, '2026-10-19T19:30:00Z', 13541132);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:27:20Z', 96, '2026-10-19T19:30:00Z', 13542326);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:31:20Z', 0, '2026-10-20T00:30:00Z', 40636);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:31:39Z', 1, '2026-10-20T00:30:00Z', 263944);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:31:53Z', 2, '2026-10-20T00:30:00Z', 361334);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:32:53Z', 3, '2026-10-20T00:30:00Z', 559719);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:33:53Z', 4, '2026-10-20T00:30:00Z', 696148);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:34:53Z', 5, '2026-10-20T00:30:00Z', 776673);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:35:53Z', 5, '2026-10-20T00:30:00Z', 834143);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:36:53Z', 6, '2026-10-20T00:30:00Z', 915678);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:38:02Z', 7, '2026-10-20T00:30:00Z', 1004372);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:38:53Z', 7, '2026-10-20T00:30:00Z', 1038820);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:39:17Z', 7, '2026-10-20T00:30:00Z', 1056020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:39:53Z', 8, '2026-10-20T00:30:00Z', 1086243);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:40:53Z', 8, '2026-10-20T00:30:00Z', 1177774);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:41:53Z', 8, '2026-10-20T00:30:00Z', 1198375);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:42:30Z', 9, '2026-10-20T00:30:00Z', 1239869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:42:53Z', 9, '2026-10-20T00:30:00Z', 1240569);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:43:53Z', 9, '2026-10-20T00:30:00Z', 1356417);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:44:19Z', 10, '2026-10-20T00:30:00Z', 1378338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:44:45Z', 10, '2026-10-20T00:30:00Z', 1401164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:45:29Z', 10, '2026-10-20T00:30:00Z', 1443505);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:45:46Z', 10, '2026-10-20T00:30:00Z', 1445471);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:46:06Z', 11, '2026-10-20T00:30:00Z', 1484741);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:46:53Z', 11, '2026-10-20T00:30:00Z', 1509441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:47:53Z', 11, '2026-10-20T00:30:00Z', 1564675);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:48:53Z', 12, '2026-10-20T00:30:00Z', 1636522);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:49:17Z', 12, '2026-10-20T00:30:00Z', 1664168);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:49:39Z', 12, '2026-10-20T00:30:00Z', 1698429);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:50:00Z', 13, '2026-10-20T00:30:00Z', 1704802);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:50:45Z', 13, '2026-10-20T00:30:00Z', 1738159);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:51:17Z', 13, '2026-10-20T00:30:00Z', 1761826);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:51:39Z', 13, '2026-10-20T00:30:00Z', 1787256);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:52:09Z', 14, '2026-10-20T00:30:00Z', 1806141);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:52:18Z', 14, '2026-10-20T00:30:00Z', 1821749);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:52:41Z', 14, '2026-10-20T00:30:00Z', 1902889);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:53:20Z', 14, '2026-10-20T00:30:00Z', 1935121);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:53:24Z', 15, '2026-10-20T00:30:00Z', 1948689);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:53:45Z', 15, '2026-10-20T00:30:00Z', 1961219);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:54:21Z', 15, '2026-10-20T00:30:00Z', 1999956);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:55:35Z', 15, '2026-10-20T00:30:00Z', 2130187);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:55:53Z', 16, '2026-10-20T00:30:00Z', 2144247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:56:17Z', 16, '2026-10-20T00:30:00Z', 2162614);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:56:53Z', 17, '2026-10-20T00:30:00Z', 2195827);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:57:23Z', 17, '2026-10-20T00:30:00Z', 2202739);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:58:16Z', 17, '2026-10-20T00:30:00Z', 2232918);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T19:59:17Z', 18, '2026-10-20T00:30:00Z', 2250673);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:00:38Z', 18, '2026-10-20T00:30:00Z', 2262203);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:01:38Z', 18, '2026-10-20T00:30:00Z', 2275119);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:02:38Z', 18, '2026-10-20T00:30:00Z', 2292450);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:03:38Z', 20, '2026-10-20T00:30:00Z', 2790734);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:04:38Z', 20, '2026-10-20T00:30:00Z', 2807128);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:05:38Z', 21, '2026-10-20T00:30:00Z', 2820893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:06:19Z', 21, '2026-10-20T00:30:00Z', 2837311);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:07:22Z', 22, '2026-10-20T00:30:00Z', 2897078);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:08:35Z', 22, '2026-10-20T00:30:00Z', 2932410);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:09:38Z', 24, '2026-10-20T00:30:00Z', 3375511);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:10:20Z', 24, '2026-10-20T00:30:00Z', 3383754);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:11:38Z', 24, '2026-10-20T00:30:00Z', 3386599);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:12:38Z', 24, '2026-10-20T00:30:00Z', 3389672);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:13:38Z', 24, '2026-10-20T00:30:00Z', 3397110);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:14:38Z', 25, '2026-10-20T00:30:00Z', 3416790);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:15:38Z', 25, '2026-10-20T00:30:00Z', 3426314);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:16:38Z', 25, '2026-10-20T00:30:00Z', 3426314);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:17:38Z', 25, '2026-10-20T00:30:00Z', 3426314);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:18:38Z', 25, '2026-10-20T00:30:00Z', 3431247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:19:38Z', 25, '2026-10-20T00:30:00Z', 3431247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:20:38Z', 25, '2026-10-20T00:30:00Z', 3431247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:21:38Z', 25, '2026-10-20T00:30:00Z', 3431247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:22:38Z', 25, '2026-10-20T00:30:00Z', 3431247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:23:38Z', 25, '2026-10-20T00:30:00Z', 3431247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:24:38Z', 25, '2026-10-20T00:30:00Z', 3641186);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:25:38Z', 25, '2026-10-20T00:30:00Z', 3641186);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:26:38Z', 25, '2026-10-20T00:30:00Z', 3641186);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:27:38Z', 25, '2026-10-20T00:30:00Z', 3641186);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:28:32Z', 25, '2026-10-20T00:30:00Z', 3648058);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:28:48Z', 26, '2026-10-20T00:30:00Z', 3663664);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:29:22Z', 26, '2026-10-20T00:30:00Z', 3806618);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:29:34Z', 27, '2026-10-20T00:30:00Z', 3931108);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:30:38Z', 28, '2026-10-20T00:30:00Z', 4201651);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:31:38Z', 29, '2026-10-20T00:30:00Z', 4303837);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:32:18Z', 30, '2026-10-20T00:30:00Z', 4383788);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:33:38Z', 31, '2026-10-20T00:30:00Z', 4449255);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:34:38Z', 31, '2026-10-20T00:30:00Z', 4474485);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:35:38Z', 31, '2026-10-20T00:30:00Z', 4514622);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:36:37Z', 32, '2026-10-20T00:30:00Z', 4560688);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:37:18Z', 32, '2026-10-20T00:30:00Z', 4591951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:38:17Z', 33, '2026-10-20T00:30:00Z', 4625629);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:39:38Z', 34, '2026-10-20T00:30:00Z', 4786843);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:40:38Z', 34, '2026-10-20T00:30:00Z', 4828146);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:41:39Z', 34, '2026-10-20T00:30:00Z', 4849817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:42:38Z', 35, '2026-10-20T00:30:00Z', 4955226);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:43:38Z', 36, '2026-10-20T00:30:00Z', 5105585);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:44:36Z', 36, '2026-10-20T00:30:00Z', 5146678);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:45:28Z', 36, '2026-10-20T00:30:00Z', 5154596);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:46:38Z', 36, '2026-10-20T00:30:00Z', 5160746);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:47:39Z', 37, '2026-10-20T00:30:00Z', 5355156);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:48:38Z', 37, '2026-10-20T00:30:00Z', 5360573);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:49:38Z', 37, '2026-10-20T00:30:00Z', 5362420);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:50:38Z', 37, '2026-10-20T00:30:00Z', 5366052);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:51:38Z', 38, '2026-10-20T00:30:00Z', 5485544);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:52:27Z', 38, '2026-10-20T00:30:00Z', 5570136);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:53:38Z', 38, '2026-10-20T00:30:00Z', 5570136);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:54:38Z', 39, '2026-10-20T00:30:00Z', 5912616);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:55:38Z', 39, '2026-10-20T00:30:00Z', 5923289);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:56:38Z', 39, '2026-10-20T00:30:00Z', 5923289);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:57:38Z', 39, '2026-10-20T00:30:00Z', 5927805);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:58:38Z', 40, '2026-10-20T00:30:00Z', 6065750);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T20:59:38Z', 40, '2026-10-20T00:30:00Z', 6218805);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:00:38Z', 41, '2026-10-20T00:30:00Z', 6385108);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:01:38Z', 42, '2026-10-20T00:30:00Z', 6406757);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:02:38Z', 42, '2026-10-20T00:30:00Z', 6424453);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:03:38Z', 42, '2026-10-20T00:30:00Z', 6429718);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:04:38Z', 42, '2026-10-20T00:30:00Z', 6429718);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:05:38Z', 42, '2026-10-20T00:30:00Z', 6429718);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:06:38Z', 42, '2026-10-20T00:30:00Z', 6439876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:07:38Z', 42, '2026-10-20T00:30:00Z', 6439876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:08:38Z', 42, '2026-10-20T00:30:00Z', 6439876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:09:19Z', 43, '2026-10-20T00:30:00Z', 6792689);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:10:38Z', 45, '2026-10-20T00:30:00Z', 7099286);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:11:38Z', 45, '2026-10-20T00:30:00Z', 7118208);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:12:39Z', 45, '2026-10-20T00:30:00Z', 7124650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:13:39Z', 45, '2026-10-20T00:30:00Z', 7124650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:14:38Z', 45, '2026-10-20T00:30:00Z', 7124650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:15:38Z', 45, '2026-10-20T00:30:00Z', 7126527);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:16:38Z', 45, '2026-10-20T00:30:00Z', 7126527);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:17:38Z', 45, '2026-10-20T00:30:00Z', 7267031);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:18:38Z', 46, '2026-10-20T00:30:00Z', 7268639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:19:38Z', 46, '2026-10-20T00:30:00Z', 7268639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:20:38Z', 46, '2026-10-20T00:30:00Z', 7470961);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:21:38Z', 46, '2026-10-20T00:30:00Z', 7478104);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:22:38Z', 46, '2026-10-20T00:30:00Z', 7634847);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:23:38Z', 49, '2026-10-20T00:30:00Z', 8020849);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:24:39Z', 49, '2026-10-20T00:30:00Z', 8031147);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:25:39Z', 49, '2026-10-20T00:30:00Z', 8032009);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:26:39Z', 49, '2026-10-20T00:30:00Z', 8032009);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:27:38Z', 49, '2026-10-20T00:30:00Z', 8032009);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:28:38Z', 49, '2026-10-20T00:30:00Z', 8032977);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:29:38Z', 49, '2026-10-20T00:30:00Z', 8032977);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:30:38Z', 49, '2026-10-20T00:30:00Z', 8241243);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:31:38Z', 49, '2026-10-20T00:30:00Z', 8242612);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:32:38Z', 50, '2026-10-20T00:30:00Z', 8402738);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:33:38Z', 50, '2026-10-20T00:30:00Z', 8406564);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:34:38Z', 51, '2026-10-20T00:30:00Z', 8566847);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:35:38Z', 51, '2026-10-20T00:30:00Z', 8575337);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:36:38Z', 51, '2026-10-20T00:30:00Z', 8575337);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:37:38Z', 52, '2026-10-20T00:30:00Z', 8783951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:38:38Z', 52, '2026-10-20T00:30:00Z', 8783951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:39:38Z', 52, '2026-10-20T00:30:00Z', 8783951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:40:38Z', 52, '2026-10-20T00:30:00Z', 8783951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:41:38Z', 52, '2026-10-20T00:30:00Z', 8783951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:42:38Z', 52, '2026-10-20T00:30:00Z', 9043146);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:43:03Z', 53, '2026-10-20T00:30:00Z', 9193653);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:43:38Z', 53, '2026-10-20T00:30:00Z', 9408006);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:44:28Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:45:38Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:46:38Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:47:38Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:48:38Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:49:38Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:50:38Z', 54, '2026-10-20T00:30:00Z', 9414632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:51:38Z', 55, '2026-10-20T00:30:00Z', 9675790);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:52:38Z', 55, '2026-10-20T00:30:00Z', 9692649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:53:38Z', 55, '2026-10-20T00:30:00Z', 9692649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:54:38Z', 55, '2026-10-20T00:30:00Z', 9692649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:55:38Z', 55, '2026-10-20T00:30:00Z', 9692649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:56:38Z', 55, '2026-10-20T00:30:00Z', 9692649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:57:38Z', 55, '2026-10-20T00:30:00Z', 9692649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:58:38Z', 56, '2026-10-20T00:30:00Z', 9960338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T21:59:38Z', 56, '2026-10-20T00:30:00Z', 9960338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:00:38Z', 56, '2026-10-20T00:30:00Z', 9960338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:01:38Z', 56, '2026-10-20T00:30:00Z', 9960338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:02:38Z', 56, '2026-10-20T00:30:00Z', 9960338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:03:38Z', 56, '2026-10-20T00:30:00Z', 9961817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:04:38Z', 56, '2026-10-20T00:30:00Z', 9961817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:05:38Z', 56, '2026-10-20T00:30:00Z', 9961817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:06:38Z', 56, '2026-10-20T00:30:00Z', 9961817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:07:38Z', 56, '2026-10-20T00:30:00Z', 9964393);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:08:38Z', 56, '2026-10-20T00:30:00Z', 9964393);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:09:38Z', 56, '2026-10-20T00:30:00Z', 9964393);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:10:38Z', 56, '2026-10-20T00:30:00Z', 9966164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:11:38Z', 56, '2026-10-20T00:30:00Z', 9966164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:12:38Z', 56, '2026-10-20T00:30:00Z', 9966164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:13:38Z', 56, '2026-10-20T00:30:00Z', 9966164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:14:38Z', 56, '2026-10-20T00:30:00Z', 9966164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:15:25Z', 56, '2026-10-20T00:30:00Z', 9970061);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:16:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:17:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:18:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:19:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:20:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:21:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:22:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:23:38Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:25:36Z', 56, '2026-10-20T00:30:00Z', 9973279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:27:25Z', 57, '2026-10-20T00:30:00Z', 10132667);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:29:08Z', 0, '2026-10-20T03:20:00Z', 8985);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:30:09Z', 1, '2026-10-20T03:20:00Z', 75363);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:31:26Z', 2, '2026-10-20T03:20:00Z', 239507);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:33:26Z', 3, '2026-10-20T03:20:00Z', 319769);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:34:40Z', 4, '2026-10-20T03:20:00Z', 524683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:36:30Z', 5, '2026-10-20T03:20:00Z', 701557);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:37:31Z', 6, '2026-10-20T03:20:00Z', 860279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:40:26Z', 7, '2026-10-20T03:20:00Z', 1204028);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:41:26Z', 8, '2026-10-20T03:20:00Z', 1265015);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:44:51Z', 9, '2026-10-20T03:20:00Z', 1516053);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:46:44Z', 10, '2026-10-20T03:20:00Z', 1973238);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:47:32Z', 11, '2026-10-20T03:20:00Z', 2004245);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:50:55Z', 12, '2026-10-20T03:20:00Z', 2155428);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:53:58Z', 0, '2026-10-20T03:50:00Z', 8643);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:54:08Z', 1, '2026-10-20T03:50:00Z', 10484);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:55:03Z', 7, '2026-10-20T03:50:00Z', 364155);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:55:18Z', 8, '2026-10-20T03:50:00Z', 372335);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:56:38Z', 9, '2026-10-20T03:50:00Z', 435972);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:57:11Z', 10, '2026-10-20T03:50:00Z', 439808);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:57:52Z', 11, '2026-10-20T03:50:00Z', 507756);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:58:41Z', 12, '2026-10-20T03:50:00Z', 525225);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:58:59Z', 14, '2026-10-20T03:50:00Z', 674012);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T22:59:43Z', 15, '2026-10-20T03:50:00Z', 702537);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:01:38Z', 20, '2026-10-20T03:50:00Z', 1028917);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:03:30Z', 21, '2026-10-20T03:50:00Z', 1079436);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:06:38Z', 23, '2026-10-20T03:50:00Z', 1231421);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:10:38Z', 24, '2026-10-20T03:50:00Z', 1233206);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:13:57Z', 25, '2026-10-20T03:50:00Z', 1253705);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:16:38Z', 26, '2026-10-20T03:50:00Z', 1266921);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:21:58Z', 27, '2026-10-20T03:50:00Z', 1344593);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:23:38Z', 30, '2026-10-20T03:50:00Z', 1516128);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:27:41Z', 31, '2026-10-20T03:50:00Z', 1520508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:31:53Z', 31, '2026-10-20T03:50:00Z', 1530973);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:34:27Z', 36, '2026-10-20T03:50:00Z', 1916146);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:34:42Z', 37, '2026-10-20T03:50:00Z', 1916146);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:38:38Z', 40, '2026-10-20T03:50:00Z', 2107340);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:42:56Z', 40, '2026-10-20T03:50:00Z', 2120005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:47:08Z', 41, '2026-10-20T03:50:00Z', 2127695);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:51:11Z', 42, '2026-10-20T03:50:00Z', 2162697);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:55:22Z', 43, '2026-10-20T03:50:00Z', 2177630);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-19T23:58:07Z', 44, '2026-10-20T03:50:00Z', 2228013);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:02:07Z', 44, '2026-10-20T03:50:00Z', 2239819);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:05:47Z', 45, '2026-10-20T03:50:00Z', 2254012);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:09:49Z', 46, '2026-10-20T03:50:00Z', 2327456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:13:07Z', 47, '2026-10-20T03:50:00Z', 2362616);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:17:08Z', 48, '2026-10-20T03:50:00Z', 2383247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:21:07Z', 49, '2026-10-20T03:50:00Z', 2416026);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:23:07Z', 50, '2026-10-20T03:50:00Z', 2443473);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:27:18Z', 51, '2026-10-20T03:50:00Z', 2475205);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:31:43Z', 52, '2026-10-20T03:50:00Z', 2520780);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:35:51Z', 53, '2026-10-20T03:50:00Z', 2553765);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:40:00Z', 53, '2026-10-20T03:50:00Z', 2567067);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:41:07Z', 54, '2026-10-20T03:50:00Z', 2588745);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:43:07Z', 55, '2026-10-20T03:50:00Z', 2652041);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:44:07Z', 56, '2026-10-20T03:50:00Z', 2666677);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:46:07Z', 57, '2026-10-20T03:50:00Z', 2691635);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:48:07Z', 58, '2026-10-20T03:50:00Z', 2705499);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:51:29Z', 0, '2026-10-20T05:50:00Z', 5098);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:52:12Z', 1, '2026-10-20T05:50:00Z', 234750);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:52:39Z', 2, '2026-10-20T05:50:00Z', 411138);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:52:57Z', 3, '2026-10-20T05:50:00Z', 632298);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:53:13Z', 4, '2026-10-20T05:50:00Z', 789436);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:53:42Z', 5, '2026-10-20T05:50:00Z', 1038643);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:54:13Z', 6, '2026-10-20T05:50:00Z', 1171209);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:55:02Z', 7, '2026-10-20T05:50:00Z', 1283015);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:56:26Z', 8, '2026-10-20T05:50:00Z', 1407713);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:58:09Z', 9, '2026-10-20T05:50:00Z', 1544478);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T00:59:52Z', 10, '2026-10-20T05:50:00Z', 1667803);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:03:21Z', 11, '2026-10-20T05:50:00Z', 1773328);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:05:28Z', 12, '2026-10-20T05:50:00Z', 1893895);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:07:25Z', 13, '2026-10-20T05:50:00Z', 2063939);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:08:55Z', 14, '2026-10-20T05:50:00Z', 2345970);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:10:25Z', 15, '2026-10-20T05:50:00Z', 2499057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:12:38Z', 16, '2026-10-20T05:50:00Z', 2559452);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:16:35Z', 17, '2026-10-20T05:50:00Z', 2771383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:20:02Z', 18, '2026-10-20T05:50:00Z', 2947596);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:23:01Z', 19, '2026-10-20T05:50:00Z', 3311782);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:27:02Z', 20, '2026-10-20T05:50:00Z', 3422701);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:29:01Z', 21, '2026-10-20T05:50:00Z', 3883070);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:30:07Z', 22, '2026-10-20T05:50:00Z', 4057377);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:34:10Z', 23, '2026-10-20T05:50:00Z', 4115751);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:48:39Z', 24, '2026-10-20T05:50:00Z', 4576851);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:52:03Z', 25, '2026-10-20T05:50:00Z', 4661577);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T01:56:04Z', 26, '2026-10-20T05:50:00Z', 4881731);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T02:00:05Z', 27, '2026-10-20T05:50:00Z', 5016469);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T02:04:18Z', 28, '2026-10-20T05:50:00Z', 5157731);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T02:10:41Z', 28, '2026-10-20T05:50:00Z', 5161988);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T02:28:41Z', 28, '2026-10-20T05:50:00Z', 5229634);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T02:32:49Z', 28, '2026-10-20T05:50:00Z', 5292382);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T02:33:51Z', 0, '2026-10-20T07:30:00Z', 1692);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T03:50:05Z', 0, '2026-10-20T08:50:00Z', 5092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T03:50:13Z', 0, '2026-10-20T08:50:00Z', 12281);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T03:50:57Z', 0, '2026-10-20T08:50:00Z', 30673);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T03:51:07Z', 0, '2026-10-20T08:50:00Z', 31519);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T03:51:53Z', 1, '2026-10-20T08:50:00Z', 50667);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T03:55:54Z', 1, '2026-10-20T08:50:00Z', 58888);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:21:50Z', 1, '2026-10-20T08:50:00Z', 60463);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:22:19Z', 2, '2026-10-20T08:50:00Z', 256724);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:22:41Z', 3, '2026-10-20T08:50:00Z', 497358);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:23:14Z', 4, '2026-10-20T08:50:00Z', 698969);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:23:40Z', 5, '2026-10-20T08:50:00Z', 897578);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:24:00Z', 6, '2026-10-20T08:50:00Z', 1033777);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:24:32Z', 7, '2026-10-20T08:50:00Z', 1181773);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:25:52Z', 8, '2026-10-20T08:50:00Z', 1357689);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:27:00Z', 9, '2026-10-20T08:50:00Z', 1432222);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:31:06Z', 10, '2026-10-20T08:50:00Z', 1543278);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:33:52Z', 11, '2026-10-20T08:50:00Z', 1797775);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:36:18Z', 12, '2026-10-20T08:50:00Z', 2129882);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:37:40Z', 13, '2026-10-20T08:50:00Z', 2234987);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:38:26Z', 14, '2026-10-20T08:50:00Z', 2439380);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:39:03Z', 15, '2026-10-20T08:50:00Z', 2619378);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:39:38Z', 16, '2026-10-20T08:50:00Z', 2846826);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:40:06Z', 17, '2026-10-20T08:50:00Z', 2937227);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:42:26Z', 18, '2026-10-20T08:50:00Z', 3070691);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:46:26Z', 19, '2026-10-20T08:50:00Z', 3382672);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:47:26Z', 20, '2026-10-20T08:50:00Z', 3599400);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:47:52Z', 21, '2026-10-20T08:50:00Z', 3895420);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:49:26Z', 22, '2026-10-20T08:50:00Z', 4135382);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:50:26Z', 23, '2026-10-20T08:50:00Z', 4274668);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:52:26Z', 24, '2026-10-20T08:50:00Z', 4440758);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:54:50Z', 25, '2026-10-20T08:50:00Z', 4637981);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:57:26Z', 26, '2026-10-20T08:50:00Z', 4710850);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T04:59:24Z', 27, '2026-10-20T08:50:00Z', 4861380);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:03:39Z', 28, '2026-10-20T08:50:00Z', 4942297);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:07:49Z', 29, '2026-10-20T08:50:00Z', 5013601);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:11:50Z', 29, '2026-10-20T08:50:00Z', 5054162);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:15:55Z', 30, '2026-10-20T08:50:00Z', 5295701);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:20:10Z', 30, '2026-10-20T08:50:00Z', 5518829);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:28:25Z', 31, '2026-10-20T08:50:00Z', 5551175);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:32:40Z', 31, '2026-10-20T08:50:00Z', 5612204);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:36:43Z', 32, '2026-10-20T08:50:00Z', 5712742);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:40:48Z', 32, '2026-10-20T08:50:00Z', 5728722);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:44:49Z', 33, '2026-10-20T08:50:00Z', 5981683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:46:31Z', 35, '2026-10-20T08:50:00Z', 6597197);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:47:51Z', 36, '2026-10-20T08:50:00Z', 6688230);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:50:35Z', 37, '2026-10-20T08:50:00Z', 6862909);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:54:45Z', 38, '2026-10-20T08:50:00Z', 6995464);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T05:59:00Z', 39, '2026-10-20T08:50:00Z', 7167185);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:03:06Z', 40, '2026-10-20T08:50:00Z', 7336325);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:07:18Z', 41, '2026-10-20T08:50:00Z', 7742810);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:11:10Z', 42, '2026-10-20T08:50:00Z', 8342942);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:13:05Z', 43, '2026-10-20T08:50:00Z', 8412594);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:16:39Z', 44, '2026-10-20T08:50:00Z', 8562508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:19:05Z', 45, '2026-10-20T08:50:00Z', 8796172);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:23:10Z', 46, '2026-10-20T08:50:00Z', 8924733);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:27:16Z', 47, '2026-10-20T08:50:00Z', 9328774);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:31:23Z', 48, '2026-10-20T08:50:00Z', 9618664);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:35:35Z', 48, '2026-10-20T08:50:00Z', 9789695);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:37:39Z', 49, '2026-10-20T08:50:00Z', 10131518);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:41:45Z', 49, '2026-10-20T08:50:00Z', 10219054);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:45:51Z', 50, '2026-10-20T08:50:00Z', 10287169);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:50:03Z', 51, '2026-10-20T08:50:00Z', 10651378);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:54:10Z', 51, '2026-10-20T08:50:00Z', 10721677);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T06:58:15Z', 52, '2026-10-20T08:50:00Z', 11092677);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T07:02:20Z', 52, '2026-10-20T08:50:00Z', 11095634);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T07:06:52Z', 53, '2026-10-20T08:50:00Z', 11494378);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T07:11:00Z', 53, '2026-10-20T08:50:00Z', 11573529);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:33:09Z', 0, '2026-10-20T15:30:00Z', 107776);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:33:22Z', 0, '2026-10-20T15:30:00Z', 113963);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:33:45Z', 0, '2026-10-20T15:30:00Z', 118479);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:33:58Z', 1, '2026-10-20T15:30:00Z', 174773);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:34:49Z', 1, '2026-10-20T15:30:00Z', 437853);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:34:55Z', 2, '2026-10-20T15:30:00Z', 439397);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:35:40Z', 2, '2026-10-20T15:30:00Z', 643597);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:36:39Z', 2, '2026-10-20T15:30:00Z', 649359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:37:39Z', 2, '2026-10-20T15:30:00Z', 649359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:38:39Z', 2, '2026-10-20T15:30:00Z', 649359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:39:39Z', 2, '2026-10-20T15:30:00Z', 649359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:40:39Z', 2, '2026-10-20T15:30:00Z', 738430);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:40:56Z', 3, '2026-10-20T15:30:00Z', 869608);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:41:56Z', 5, '2026-10-20T15:30:00Z', 1222097);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:42:50Z', 6, '2026-10-20T15:30:00Z', 1432094);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:43:56Z', 7, '2026-10-20T15:30:00Z', 1504593);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:44:56Z', 7, '2026-10-20T15:30:00Z', 1577673);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:45:56Z', 8, '2026-10-20T15:30:00Z', 1604816);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:46:56Z', 8, '2026-10-20T15:30:00Z', 1624535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:47:56Z', 8, '2026-10-20T15:30:00Z', 1652535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:48:56Z', 8, '2026-10-20T15:30:00Z', 1683966);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:49:56Z', 9, '2026-10-20T15:30:00Z', 1715200);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:50:56Z', 9, '2026-10-20T15:30:00Z', 1745119);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:51:56Z', 9, '2026-10-20T15:30:00Z', 1774144);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:52:56Z', 9, '2026-10-20T15:30:00Z', 1793153);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:53:56Z', 9, '2026-10-20T15:30:00Z', 1795211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:54:56Z', 9, '2026-10-20T15:30:00Z', 1795211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:55:56Z', 9, '2026-10-20T15:30:00Z', 1795211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:56:56Z', 9, '2026-10-20T15:30:00Z', 1795211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:57:56Z', 9, '2026-10-20T15:30:00Z', 1801326);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:58:56Z', 10, '2026-10-20T15:30:00Z', 1808680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T10:59:56Z', 10, '2026-10-20T15:30:00Z', 1808680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:00:56Z', 10, '2026-10-20T15:30:00Z', 1808680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:01:56Z', 10, '2026-10-20T15:30:00Z', 1808680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:02:56Z', 10, '2026-10-20T15:30:00Z', 1808680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:03:56Z', 10, '2026-10-20T15:30:00Z', 1808680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:04:56Z', 10, '2026-10-20T15:30:00Z', 2009892);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:05:56Z', 10, '2026-10-20T15:30:00Z', 2024513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:06:56Z', 10, '2026-10-20T15:30:00Z', 2024513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:07:56Z', 10, '2026-10-20T15:30:00Z', 2024513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:08:56Z', 10, '2026-10-20T15:30:00Z', 2024513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:09:56Z', 10, '2026-10-20T15:30:00Z', 2024513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:10:56Z', 10, '2026-10-20T15:30:00Z', 2024513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:11:56Z', 10, '2026-10-20T15:30:00Z', 2233216);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:12:56Z', 12, '2026-10-20T15:30:00Z', 2464094);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:13:56Z', 13, '2026-10-20T15:30:00Z', 2626903);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:14:56Z', 14, '2026-10-20T15:30:00Z', 2798246);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:15:56Z', 15, '2026-10-20T15:30:00Z', 2885334);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:16:56Z', 15, '2026-10-20T15:30:00Z', 2955617);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:17:56Z', 16, '2026-10-20T15:30:00Z', 3011042);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:18:56Z', 17, '2026-10-20T15:30:00Z', 3247408);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:19:56Z', 17, '2026-10-20T15:30:00Z', 3288102);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:20:56Z', 18, '2026-10-20T15:30:00Z', 3315926);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:21:56Z', 18, '2026-10-20T15:30:00Z', 3319683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:22:56Z', 18, '2026-10-20T15:30:00Z', 3319683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:23:56Z', 18, '2026-10-20T15:30:00Z', 3319683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:24:56Z', 18, '2026-10-20T15:30:00Z', 3319683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:25:56Z', 18, '2026-10-20T15:30:00Z', 3319683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:26:56Z', 18, '2026-10-20T15:30:00Z', 3319683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:27:56Z', 19, '2026-10-20T15:30:00Z', 3598191);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:28:56Z', 19, '2026-10-20T15:30:00Z', 3598191);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:29:56Z', 19, '2026-10-20T15:30:00Z', 3598191);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:30:56Z', 19, '2026-10-20T15:30:00Z', 3598191);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:31:56Z', 19, '2026-10-20T15:30:00Z', 3598191);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:32:56Z', 19, '2026-10-20T15:30:00Z', 3598191);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:33:56Z', 19, '2026-10-20T15:30:00Z', 3877079);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:34:56Z', 20, '2026-10-20T15:30:00Z', 3982113);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:35:56Z', 22, '2026-10-20T15:30:00Z', 4380611);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:36:56Z', 23, '2026-10-20T15:30:00Z', 4468499);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:37:56Z', 23, '2026-10-20T15:30:00Z', 4550789);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:38:56Z', 23, '2026-10-20T15:30:00Z', 4573801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:39:57Z', 24, '2026-10-20T15:30:00Z', 4853037);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:40:57Z', 25, '2026-10-20T15:30:00Z', 4861911);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:41:57Z', 25, '2026-10-20T15:30:00Z', 4861911);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:42:57Z', 25, '2026-10-20T15:30:00Z', 4861911);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:43:57Z', 25, '2026-10-20T15:30:00Z', 4861911);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:44:57Z', 25, '2026-10-20T15:30:00Z', 4863616);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:45:57Z', 25, '2026-10-20T15:30:00Z', 4863616);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:46:57Z', 25, '2026-10-20T15:30:00Z', 4960475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:47:26Z', 25, '2026-10-20T15:30:00Z', 4960475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:48:57Z', 25, '2026-10-20T15:30:00Z', 4964893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:49:57Z', 25, '2026-10-20T15:30:00Z', 4965876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:50:57Z', 25, '2026-10-20T15:30:00Z', 4965876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:51:57Z', 25, '2026-10-20T15:30:00Z', 4965876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:52:57Z', 25, '2026-10-20T15:30:00Z', 4965876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:53:57Z', 25, '2026-10-20T15:30:00Z', 4965876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:54:57Z', 25, '2026-10-20T15:30:00Z', 4965876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:55:57Z', 26, '2026-10-20T15:30:00Z', 5370313);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:56:57Z', 27, '2026-10-20T15:30:00Z', 5448909);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:57:57Z', 27, '2026-10-20T15:30:00Z', 5477770);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:58:57Z', 27, '2026-10-20T15:30:00Z', 5507785);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T11:59:31Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:00:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:01:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:02:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:03:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:04:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:05:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:06:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:07:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:08:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:09:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:10:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:11:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:12:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:13:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:14:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:15:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:16:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:17:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:18:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:19:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:20:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:21:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:22:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:23:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:24:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:25:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:26:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:27:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:28:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:29:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:30:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:31:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:32:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:33:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:34:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:35:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:36:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:37:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:38:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:39:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:40:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:41:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:42:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:43:57Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:44:34Z', 28, '2026-10-20T15:30:00Z', 5640863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:45:20Z', 28, '2026-10-20T15:30:00Z', 5807338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:46:17Z', 28, '2026-10-20T15:30:00Z', 5860120);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:46:27Z', 29, '2026-10-20T15:30:00Z', 5895855);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:48:30Z', 30, '2026-10-20T15:30:00Z', 6012605);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:52:41Z', 31, '2026-10-20T15:30:00Z', 6080417);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:55:04Z', 33, '2026-10-20T15:30:00Z', 6451280);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:55:31Z', 34, '2026-10-20T15:30:00Z', 6609599);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:55:45Z', 35, '2026-10-20T15:30:00Z', 6894854);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:56:08Z', 36, '2026-10-20T15:30:00Z', 7112353);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:57:04Z', 37, '2026-10-20T15:30:00Z', 7348575);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:58:04Z', 38, '2026-10-20T15:30:00Z', 7449438);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T12:59:09Z', 39, '2026-10-20T15:30:00Z', 7538245);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:00:22Z', 40, '2026-10-20T15:30:00Z', 7634040);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:03:08Z', 41, '2026-10-20T15:30:00Z', 7901017);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:03:33Z', 42, '2026-10-20T15:30:00Z', 8019993);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:04:33Z', 43, '2026-10-20T15:30:00Z', 8154760);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:06:08Z', 45, '2026-10-20T15:30:00Z', 8387365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:07:08Z', 46, '2026-10-20T15:30:00Z', 8658588);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:08:08Z', 47, '2026-10-20T15:30:00Z', 8804802);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:12:08Z', 48, '2026-10-20T15:30:00Z', 9088160);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:13:08Z', 49, '2026-10-20T15:30:00Z', 9174391);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:14:33Z', 50, '2026-10-20T15:30:00Z', 9375704);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:17:08Z', 51, '2026-10-20T15:30:00Z', 9615603);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:18:08Z', 52, '2026-10-20T15:30:00Z', 9690347);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:21:08Z', 53, '2026-10-20T15:30:00Z', 9808582);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:22:28Z', 54, '2026-10-20T15:30:00Z', 10005376);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:24:08Z', 55, '2026-10-20T15:30:00Z', 10179812);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:25:08Z', 56, '2026-10-20T15:30:00Z', 10313639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:26:08Z', 57, '2026-10-20T15:30:00Z', 10566859);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:26:16Z', 58, '2026-10-20T15:30:00Z', 10588310);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:29:08Z', 59, '2026-10-20T15:30:00Z', 10826683);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:30:33Z', 60, '2026-10-20T15:30:00Z', 10875108);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:34:24Z', 61, '2026-10-20T15:30:00Z', 11080946);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:37:08Z', 62, '2026-10-20T15:30:00Z', 11319584);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:38:08Z', 63, '2026-10-20T15:30:00Z', 11389113);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:38:09Z', 63, '2026-10-20T15:30:00Z', 11389113);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:39:47Z', 64, '2026-10-20T15:30:00Z', 11489117);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:41:14Z', 65, '2026-10-20T15:30:00Z', 11676241);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:45:22Z', 66, '2026-10-20T15:30:00Z', 11845772);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:49:30Z', 66, '2026-10-20T15:30:00Z', 11980288);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:51:03Z', 67, '2026-10-20T15:30:00Z', 12058308);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:52:33Z', 68, '2026-10-20T15:30:00Z', 12283456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T13:56:33Z', 69, '2026-10-20T15:30:00Z', 12414979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:00:48Z', 70, '2026-10-20T15:30:00Z', 12604360);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:06:50Z', 70, '2026-10-20T15:30:00Z', 12804828);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:09:33Z', 71, '2026-10-20T15:30:00Z', 12910397);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:13:50Z', 72, '2026-10-20T15:30:00Z', 12998852);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:17:59Z', 73, '2026-10-20T15:30:00Z', 13074383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:22:05Z', 73, '2026-10-20T15:30:00Z', 13107776);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:27:20Z', 74, '2026-10-20T15:30:00Z', 13342078);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:31:21Z', 74, '2026-10-20T15:30:00Z', 13358583);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:35:59Z', 75, '2026-10-20T15:30:00Z', 13604318);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:39:33Z', 76, '2026-10-20T15:30:00Z', 13793549);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:43:33Z', 77, '2026-10-20T15:30:00Z', 13883987);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:47:39Z', 77, '2026-10-20T15:30:00Z', 13902111);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:52:13Z', 78, '2026-10-20T15:30:00Z', 14164680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T14:56:25Z', 79, '2026-10-20T15:30:00Z', 14200634);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:00:27Z', 79, '2026-10-20T15:30:00Z', 14205563);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:04:42Z', 79, '2026-10-20T15:30:00Z', 14214053);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:10:39Z', 79, '2026-10-20T15:30:00Z', 14510084);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:11:28Z', 80, '2026-10-20T15:30:00Z', 14524663);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:15:33Z', 81, '2026-10-20T15:30:00Z', 14641444);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:19:46Z', 81, '2026-10-20T15:30:00Z', 14645979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:23:53Z', 82, '2026-10-20T15:30:00Z', 14910539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:27:59Z', 83, '2026-10-20T15:30:00Z', 15109835);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:30:01Z', 0, '2026-10-20T20:30:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:32:33Z', 1, '2026-10-20T20:30:00Z', 86872);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:36:42Z', 1, '2026-10-20T20:30:00Z', 108512);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:43:07Z', 1, '2026-10-20T20:30:00Z', 228904);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:47:17Z', 2, '2026-10-20T20:30:00Z', 243176);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:51:21Z', 2, '2026-10-20T20:30:00Z', 319778);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:53:33Z', 3, '2026-10-20T20:30:00Z', 477047);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:56:45Z', 4, '2026-10-20T20:30:00Z', 721475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:57:33Z', 5, '2026-10-20T20:30:00Z', 810784);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:57:40Z', 5, '2026-10-20T20:30:00Z', 828385);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T15:59:30Z', 6, '2026-10-20T20:30:00Z', 961361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:00:44Z', 7, '2026-10-20T20:30:00Z', 1128584);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:02:44Z', 8, '2026-10-20T20:30:00Z', 1277260);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:03:44Z', 9, '2026-10-20T20:30:00Z', 1345185);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:05:17Z', 10, '2026-10-20T20:30:00Z', 1576524);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:08:50Z', 11, '2026-10-20T20:30:00Z', 1664869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:09:00Z', 11, '2026-10-20T20:30:00Z', 1676429);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:10:44Z', 12, '2026-10-20T20:30:00Z', 1733831);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:12:52Z', 13, '2026-10-20T20:30:00Z', 1945415);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:13:32Z', 14, '2026-10-20T20:30:00Z', 1996946);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:14:40Z', 15, '2026-10-20T20:30:00Z', 2080572);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:17:27Z', 16, '2026-10-20T20:30:00Z', 2229651);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:18:52Z', 17, '2026-10-20T20:30:00Z', 2403179);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:19:00Z', 17, '2026-10-20T20:30:00Z', 2420042);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:19:52Z', 18, '2026-10-20T20:30:00Z', 2538871);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:21:52Z', 20, '2026-10-20T20:30:00Z', 2979426);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:23:37Z', 21, '2026-10-20T20:30:00Z', 3109375);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:27:45Z', 21, '2026-10-20T20:30:00Z', 3124745);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:29:46Z', 22, '2026-10-20T20:30:00Z', 3328356);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:32:48Z', 23, '2026-10-20T20:30:00Z', 3497636);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:32:49Z', 23, '2026-10-20T20:30:00Z', 3504373);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:33:33Z', 24, '2026-10-20T20:30:00Z', 3675946);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:34:52Z', 25, '2026-10-20T20:30:00Z', 3850777);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:35:52Z', 27, '2026-10-20T20:30:00Z', 4152099);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:37:52Z', 28, '2026-10-20T20:30:00Z', 4361973);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:38:51Z', 29, '2026-10-20T20:30:00Z', 4389402);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:38:56Z', 29, '2026-10-20T20:30:00Z', 4391437);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:43:00Z', 29, '2026-10-20T20:30:00Z', 4422390);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:47:01Z', 30, '2026-10-20T20:30:00Z', 4543831);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:51:11Z', 31, '2026-10-20T20:30:00Z', 4779787);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:57:54Z', 31, '2026-10-20T20:30:00Z', 5020807);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T16:59:48Z', 32, '2026-10-20T20:30:00Z', 5168313);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:01:52Z', 33, '2026-10-20T20:30:00Z', 5271006);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:03:52Z', 34, '2026-10-20T20:30:00Z', 5442201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:08:05Z', 35, '2026-10-20T20:30:00Z', 5517898);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:12:07Z', 35, '2026-10-20T20:30:00Z', 5534893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:12:52Z', 36, '2026-10-20T20:30:00Z', 5793773);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:17:05Z', 36, '2026-10-20T20:30:00Z', 5804217);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:18:52Z', 37, '2026-10-20T20:30:00Z', 6029966);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:22:52Z', 38, '2026-10-20T20:30:00Z', 6308031);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:26:54Z', 38, '2026-10-20T20:30:00Z', 6312641);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:31:03Z', 38, '2026-10-20T20:30:00Z', 6355655);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:31:52Z', 39, '2026-10-20T20:30:00Z', 6489332);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:34:53Z', 40, '2026-10-20T20:30:00Z', 6596550);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:37:59Z', 41, '2026-10-20T20:30:00Z', 6731042);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:40:49Z', 42, '2026-10-20T20:30:00Z', 6956914);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:40:59Z', 43, '2026-10-20T20:30:00Z', 6969188);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:44:52Z', 44, '2026-10-20T20:30:00Z', 7228058);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:49:04Z', 45, '2026-10-20T20:30:00Z', 7296988);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:52:52Z', 46, '2026-10-20T20:30:00Z', 7600097);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T17:57:00Z', 47, '2026-10-20T20:30:00Z', 7697566);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:00:58Z', 48, '2026-10-20T20:30:00Z', 7794443);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:03:50Z', 49, '2026-10-20T20:30:00Z', 8003288);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:04:52Z', 51, '2026-10-20T20:30:00Z', 8480548);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:05:52Z', 52, '2026-10-20T20:30:00Z', 8748612);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:06:52Z', 54, '2026-10-20T20:30:00Z', 8932239);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:07:02Z', 54, '2026-10-20T20:30:00Z', 8932239);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:07:52Z', 55, '2026-10-20T20:30:00Z', 9007879);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:09:38Z', 56, '2026-10-20T20:30:00Z', 9076456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:13:40Z', 57, '2026-10-20T20:30:00Z', 9134361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:16:31Z', 58, '2026-10-20T20:30:00Z', 9307324);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:17:52Z', 59, '2026-10-20T20:30:00Z', 9506219);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:18:04Z', 60, '2026-10-20T20:30:00Z', 9537197);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:19:58Z', 61, '2026-10-20T20:30:00Z', 9706941);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:22:52Z', 62, '2026-10-20T20:30:00Z', 9820893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:24:12Z', 63, '2026-10-20T20:30:00Z', 9869623);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:27:25Z', 64, '2026-10-20T20:30:00Z', 9961719);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:29:29Z', 65, '2026-10-20T20:30:00Z', 10168288);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:32:06Z', 66, '2026-10-20T20:30:00Z', 10266656);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:32:10Z', 66, '2026-10-20T20:30:00Z', 10268808);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:33:02Z', 67, '2026-10-20T20:30:00Z', 10484219);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:35:50Z', 68, '2026-10-20T20:30:00Z', 10650403);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:40:01Z', 69, '2026-10-20T20:30:00Z', 10811662);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:44:04Z', 70, '2026-10-20T20:30:00Z', 10878858);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:47:52Z', 71, '2026-10-20T20:30:00Z', 11164842);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:48:52Z', 72, '2026-10-20T20:30:00Z', 11331849);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:48:53Z', 72, '2026-10-20T20:30:00Z', 11331849);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:53:08Z', 73, '2026-10-20T20:30:00Z', 11389060);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T18:57:14Z', 74, '2026-10-20T20:30:00Z', 11498085);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:01:17Z', 75, '2026-10-20T20:30:00Z', 11583175);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:05:22Z', 75, '2026-10-20T20:30:00Z', 11656980);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:08:49Z', 76, '2026-10-20T20:30:00Z', 11853239);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:10:58Z', 77, '2026-10-20T20:30:00Z', 11951609);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:15:06Z', 78, '2026-10-20T20:30:00Z', 12023791);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:19:09Z', 79, '2026-10-20T20:30:00Z', 12269518);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:23:21Z', 80, '2026-10-20T20:30:00Z', 12339122);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:23:58Z', 80, '2026-10-20T20:30:00Z', 12345534);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:26:13Z', 81, '2026-10-20T20:30:00Z', 12476059);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:30:16Z', 82, '2026-10-20T20:30:00Z', 12557608);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:30:52Z', 82, '2026-10-20T20:30:00Z', 12564207);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:35:00Z', 83, '2026-10-20T20:30:00Z', 12634706);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:35:58Z', 83, '2026-10-20T20:30:00Z', 12643211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:39:58Z', 84, '2026-10-20T20:30:00Z', 12839231);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:39:59Z', 84, '2026-10-20T20:30:00Z', 12839231);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:43:58Z', 85, '2026-10-20T20:30:00Z', 12936413);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:46:58Z', 86, '2026-10-20T20:30:00Z', 13016587);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:51:05Z', 87, '2026-10-20T20:30:00Z', 13090447);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:51:58Z', 87, '2026-10-20T20:30:00Z', 13106798);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T19:56:13Z', 87, '2026-10-20T20:30:00Z', 13112000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:31:13Z', 0, '2026-10-21T01:30:00Z', 843);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:31:24Z', 0, '2026-10-21T01:30:00Z', 38637);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:31:58Z', 1, '2026-10-21T01:30:00Z', 106947);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:32:58Z', 1, '2026-10-21T01:30:00Z', 144645);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:33:58Z', 1, '2026-10-21T01:30:00Z', 179895);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:34:58Z', 1, '2026-10-21T01:30:00Z', 210998);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:35:58Z', 2, '2026-10-21T01:30:00Z', 247865);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:36:31Z', 2, '2026-10-21T01:30:00Z', 263840);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:36:58Z', 2, '2026-10-21T01:30:00Z', 302775);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:37:58Z', 3, '2026-10-21T01:30:00Z', 365922);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:38:58Z', 3, '2026-10-21T01:30:00Z', 416111);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:39:40Z', 3, '2026-10-21T01:30:00Z', 428863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:39:55Z', 3, '2026-10-21T01:30:00Z', 449194);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:40:58Z', 4, '2026-10-21T01:30:00Z', 553979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:41:58Z', 4, '2026-10-21T01:30:00Z', 570096);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:42:58Z', 4, '2026-10-21T01:30:00Z', 603736);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:43:58Z', 5, '2026-10-21T01:30:00Z', 641451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:44:58Z', 5, '2026-10-21T01:30:00Z', 678048);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:45:29.8538723Z', 6, '2026-10-21T01:30:00Z', 692769);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:45:29Z', 6, '2026-10-21T01:30:00Z', 692769);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:47:59Z', 6, '2026-10-21T01:30:00Z', 742060);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:48:58Z', 6, '2026-10-21T01:30:00Z', 756415);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:49:35.5434058Z', 7, '2026-10-21T01:30:00Z', 792327);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:49:58Z', 7, '2026-10-21T01:30:00Z', 814411);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:50:58Z', 7, '2026-10-21T01:30:00Z', 885080);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:51:58Z', 7, '2026-10-21T01:30:00Z', 914771);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:52:58Z', 8, '2026-10-21T01:30:00Z', 960808);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:53:58Z', 8, '2026-10-21T01:30:00Z', 974803);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:54:58Z', 8, '2026-10-21T01:30:00Z', 1004000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:55:59Z', 9, '2026-10-21T01:30:00Z', 1032602);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:56:05.256395Z', 9, '2026-10-21T01:30:00Z', 1034390);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:56:05Z', 9, '2026-10-21T01:30:00Z', 1034390);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:56:59Z', 9, '2026-10-21T01:30:00Z', 1049204);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:57:58Z', 9, '2026-10-21T01:30:00Z', 1060802);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:58:36Z', 9, '2026-10-21T01:30:00Z', 1072774);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:58:50Z', 9, '2026-10-21T01:30:00Z', 1084448);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:58:58Z', 9, '2026-10-21T01:30:00Z', 1087104);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:59:32Z', 9, '2026-10-21T01:30:00Z', 1093438);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T20:59:58Z', 10, '2026-10-21T01:30:00Z', 1124017);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:00:58Z', 10, '2026-10-21T01:30:00Z', 1140290);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:01:58Z', 10, '2026-10-21T01:30:00Z', 1187534);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:02:58Z', 10, '2026-10-21T01:30:00Z', 1204107);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:03:58Z', 10, '2026-10-21T01:30:00Z', 1221652);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:04:58Z', 10, '2026-10-21T01:30:00Z', 1241286);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:05:58Z', 11, '2026-10-21T01:30:00Z', 1254569);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:06:58Z', 11, '2026-10-21T01:30:00Z', 1275736);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:07:58Z', 11, '2026-10-21T01:30:00Z', 1278249);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:08:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:09:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:10:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:11:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:12:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:13:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:14:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:15:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:16:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:17:58Z', 11, '2026-10-21T01:30:00Z', 1288000);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:18:58Z', 12, '2026-10-21T01:30:00Z', 1462395);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:19:58Z', 12, '2026-10-21T01:30:00Z', 1470818);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:20:54Z', 12, '2026-10-21T01:30:00Z', 1477166);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:21:58Z', 12, '2026-10-21T01:30:00Z', 1591492);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:22:58Z', 13, '2026-10-21T01:30:00Z', 1620869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:23:58Z', 13, '2026-10-21T01:30:00Z', 1665286);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:24:58Z', 13, '2026-10-21T01:30:00Z', 1700751);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:25:58Z', 14, '2026-10-21T01:30:00Z', 1715877);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:26:55Z', 14, '2026-10-21T01:30:00Z', 1749665);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:28:58Z', 14, '2026-10-21T01:30:00Z', 1788032);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:29:09.5431167Z', 15, '2026-10-21T01:30:00Z', 1791334);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:29:58Z', 15, '2026-10-21T01:30:00Z', 1797693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:30:58Z', 15, '2026-10-21T01:30:00Z', 1808886);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:31:25Z', 15, '2026-10-21T01:30:00Z', 1915551);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:31:58Z', 15, '2026-10-21T01:30:00Z', 1930911);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:32:25Z', 16, '2026-10-21T01:30:00Z', 2034435);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:32:55Z', 17, '2026-10-21T01:30:00Z', 2318279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:33:43Z', 17, '2026-10-21T01:30:00Z', 2392679);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:35:26Z', 17, '2026-10-21T01:30:00Z', 2418024);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:36:03Z', 17, '2026-10-21T01:30:00Z', 2427043);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:36:05.2482334Z', 18, '2026-10-21T01:30:00Z', 2427043);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:36:05Z', 18, '2026-10-21T01:30:00Z', 2427043);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:40:58Z', 18, '2026-10-21T01:30:00Z', 2465405);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:42:30Z', 18, '2026-10-21T01:30:00Z', 2475267);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:47:34.8131532Z', 20, '2026-10-21T01:30:00Z', 2787024);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:47:34Z', 20, '2026-10-21T01:30:00Z', 2787024);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:49:56Z', 21, '2026-10-21T01:30:00Z', 2987099);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:50:57Z', 24, '2026-10-21T01:30:00Z', 3553355);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:51:58Z', 27, '2026-10-21T01:30:00Z', 4116068);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:52:05.494447Z', 27, '2026-10-21T01:30:00Z', 4195076);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:52:05Z', 27, '2026-10-21T01:30:00Z', 4194169);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:52:59Z', 29, '2026-10-21T01:30:00Z', 4450979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:54:01Z', 30, '2026-10-21T01:30:00Z', 4652554);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:55:08Z', 31, '2026-10-21T01:30:00Z', 4750639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:55:49Z', 32, '2026-10-21T01:30:00Z', 4837123);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:57:11Z', 33, '2026-10-21T01:30:00Z', 4909120);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T21:58:11Z', 33, '2026-10-21T01:30:00Z', 5014202);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:00:17Z', 35, '2026-10-21T01:30:00Z', 5320531);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:05:30Z', 36, '2026-10-21T01:30:00Z', 5651670);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:06:07Z', 39, '2026-10-21T01:30:00Z', 5834179);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:06:31.6198936Z', 40, '2026-10-21T01:30:00Z', 5860287);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:11:10.8692303Z', 41, '2026-10-21T01:30:00Z', 5901804);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:11:10Z', 41, '2026-10-21T01:30:00Z', 5901804);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:14:30Z', 41, '2026-10-21T01:30:00Z', 6063553);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:15:49Z', 41, '2026-10-21T01:30:00Z', 6084515);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:17:05Z', 42, '2026-10-21T01:30:00Z', 6106595);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:17:18.9564029Z', 43, '2026-10-21T01:30:00Z', 6176057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:17:18Z', 43, '2026-10-21T01:30:00Z', 6176057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:20:32Z', 43, '2026-10-21T01:30:00Z', 6217008);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:20:56Z', 43, '2026-10-21T01:30:00Z', 6219742);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:21:57.7816695Z', 44, '2026-10-21T01:30:00Z', 6271081);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:21:57Z', 44, '2026-10-21T01:30:00Z', 6271081);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:24:08Z', 44, '2026-10-21T01:30:00Z', 6374629);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:25:41Z', 44, '2026-10-21T01:30:00Z', 6452980);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:26:41Z', 45, '2026-10-21T01:30:00Z', 6515933);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:26:48.0732557Z', 46, '2026-10-21T01:30:00Z', 6529025);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:30:23Z', 46, '2026-10-21T01:30:00Z', 6682680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:34:35Z', 46, '2026-10-21T01:30:00Z', 6813066);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:35:19Z', 46, '2026-10-21T01:30:00Z', 7085429);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:36:32Z', 48, '2026-10-21T01:30:00Z', 7157268);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:37:44Z', 49, '2026-10-21T01:30:00Z', 7282650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:37:47.1489759Z', 50, '2026-10-21T01:30:00Z', 7282650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:39:05Z', 50, '2026-10-21T01:30:00Z', 7314352);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:39:31Z', 50, '2026-10-21T01:30:00Z', 7320925);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:40:17Z', 50, '2026-10-21T01:30:00Z', 7409069);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:40:31Z', 50, '2026-10-21T01:30:00Z', 7417178);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:41:05Z', 50, '2026-10-21T01:30:00Z', 7426942);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:41:32Z', 51, '2026-10-21T01:30:00Z', 7431497);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:42:06Z', 51, '2026-10-21T01:30:00Z', 7527588);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:42:10.1721282Z', 52, '2026-10-21T01:30:00Z', 7528006);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:43:59Z', 52, '2026-10-21T01:30:00Z', 7619770);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:44:27Z', 52, '2026-10-21T01:30:00Z', 7746011);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:44:59Z', 52, '2026-10-21T01:30:00Z', 7757700);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:45:28Z', 52, '2026-10-21T01:30:00Z', 7773975);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:46:04Z', 52, '2026-10-21T01:30:00Z', 7780884);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:46:36Z', 52, '2026-10-21T01:30:00Z', 7783667);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:47:13Z', 54, '2026-10-21T01:30:00Z', 8195841);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:47:37Z', 54, '2026-10-21T01:30:00Z', 8212703);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:48:32Z', 54, '2026-10-21T01:30:00Z', 8221725);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:49:28Z', 54, '2026-10-21T01:30:00Z', 8232627);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:50:28Z', 54, '2026-10-21T01:30:00Z', 8239346);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:51:14Z', 55, '2026-10-21T01:30:00Z', 8629321);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:51:27Z', 55, '2026-10-21T01:30:00Z', 8631775);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:52:42Z', 56, '2026-10-21T01:30:00Z', 8892583);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:53:34Z', 56, '2026-10-21T01:30:00Z', 8980718);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:53:53Z', 57, '2026-10-21T01:30:00Z', 9190861);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:53:58Z', 57, '2026-10-21T01:30:00Z', 9190861);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:54:20Z', 58, '2026-10-21T01:30:00Z', 9192493);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:54:27Z', 58, '2026-10-21T01:30:00Z', 9198105);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:55:34Z', 58, '2026-10-21T01:30:00Z', 9249663);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:56:34Z', 58, '2026-10-21T01:30:00Z', 9260009);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:57:31Z', 58, '2026-10-21T01:30:00Z', 9266308);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:58:30Z', 58, '2026-10-21T01:30:00Z', 9279916);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T22:59:27Z', 59, '2026-10-21T01:30:00Z', 9291738);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:00:26Z', 59, '2026-10-21T01:30:00Z', 9299099);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:01:26Z', 59, '2026-10-21T01:30:00Z', 9311080);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:02:26Z', 59, '2026-10-21T01:30:00Z', 9325108);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:03:26Z', 59, '2026-10-21T01:30:00Z', 9352445);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:04:34Z', 60, '2026-10-21T01:30:00Z', 9410902);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:05:35Z', 60, '2026-10-21T01:30:00Z', 9455631);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:06:35Z', 61, '2026-10-21T01:30:00Z', 9668429);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:07:34Z', 62, '2026-10-21T01:30:00Z', 9853556);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:08:34Z', 63, '2026-10-21T01:30:00Z', 9945892);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:09:30Z', 63, '2026-10-21T01:30:00Z', 9965307);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:09:40.1479558Z', 64, '2026-10-21T01:30:00Z', 9979077);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:09:40Z', 64, '2026-10-21T01:30:00Z', 9977247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:10:59Z', 64, '2026-10-21T01:30:00Z', 10058575);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:11:26Z', 64, '2026-10-21T01:30:00Z', 10059955);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:12:34Z', 64, '2026-10-21T01:30:00Z', 10140734);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:13:08Z', 65, '2026-10-21T01:30:00Z', 10158451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:13:31Z', 65, '2026-10-21T01:30:00Z', 10160277);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:14:34Z', 65, '2026-10-21T01:30:00Z', 10176523);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:15:27Z', 65, '2026-10-21T01:30:00Z', 10197967);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:16:30Z', 65, '2026-10-21T01:30:00Z', 10222891);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:16:34Z', 66, '2026-10-21T01:30:00Z', 10227927);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:17:31Z', 66, '2026-10-21T01:30:00Z', 10247901);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:18:27Z', 66, '2026-10-21T01:30:00Z', 10262396);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:19:32Z', 66, '2026-10-21T01:30:00Z', 10276059);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:20:28Z', 66, '2026-10-21T01:30:00Z', 10278760);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:21:20Z', 67, '2026-10-21T01:30:00Z', 10317594);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:21:26Z', 67, '2026-10-21T01:30:00Z', 10345491);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:22:26Z', 67, '2026-10-21T01:30:00Z', 10439114);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:23:34Z', 68, '2026-10-21T01:30:00Z', 10498991);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:24:34Z', 68, '2026-10-21T01:30:00Z', 10512428);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:25:34Z', 68, '2026-10-21T01:30:00Z', 10514919);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:26:34Z', 68, '2026-10-21T01:30:00Z', 10521345);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:27:29Z', 68, '2026-10-21T01:30:00Z', 10608568);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:28:04Z', 69, '2026-10-21T01:30:00Z', 10643318);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:28:30Z', 69, '2026-10-21T01:30:00Z', 10657903);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:29:54Z', 69, '2026-10-21T01:30:00Z', 10695870);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:29:55.147798Z', 70, '2026-10-21T01:30:00Z', 10696642);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:29:55Z', 70, '2026-10-21T01:30:00Z', 10696642);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:31:34Z', 70, '2026-10-21T01:30:00Z', 10741983);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:32:34Z', 70, '2026-10-21T01:30:00Z', 10772868);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:33:34Z', 70, '2026-10-21T01:30:00Z', 10790163);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:34:34Z', 71, '2026-10-21T01:30:00Z', 10882908);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:35:34Z', 71, '2026-10-21T01:30:00Z', 10938965);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:36:34Z', 71, '2026-10-21T01:30:00Z', 10999318);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:37:34Z', 72, '2026-10-21T01:30:00Z', 11011887);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:38:34Z', 72, '2026-10-21T01:30:00Z', 11044991);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:39:34Z', 72, '2026-10-21T01:30:00Z', 11059149);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:40:34Z', 73, '2026-10-21T01:30:00Z', 11365143);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:41:34Z', 73, '2026-10-21T01:30:00Z', 11365143);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:42:34Z', 73, '2026-10-21T01:30:00Z', 11516555);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:43:34Z', 73, '2026-10-21T01:30:00Z', 11518446);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:43:47Z', 74, '2026-10-21T01:30:00Z', 11526576);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:44:54Z', 74, '2026-10-21T01:30:00Z', 11528916);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:45:34Z', 74, '2026-10-21T01:30:00Z', 11530742);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:46:34Z', 74, '2026-10-21T01:30:00Z', 11533020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:47:34Z', 74, '2026-10-21T01:30:00Z', 11533020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:48:34Z', 74, '2026-10-21T01:30:00Z', 11533020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:49:34Z', 74, '2026-10-21T01:30:00Z', 11533020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:50:34Z', 74, '2026-10-21T01:30:00Z', 11533020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:51:35Z', 74, '2026-10-21T01:30:00Z', 11536290);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:52:35Z', 74, '2026-10-21T01:30:00Z', 11536290);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:53:29Z', 74, '2026-10-21T01:30:00Z', 11555539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:54:01Z', 75, '2026-10-21T01:30:00Z', 11678956);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:54:55Z', 75, '2026-10-21T01:30:00Z', 11770379);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:55:08.0441133Z', 76, '2026-10-21T01:30:00Z', 11798178);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:55:08Z', 76, '2026-10-21T01:30:00Z', 11798178);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:55:55Z', 76, '2026-10-21T01:30:00Z', 11853361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:56:55Z', 76, '2026-10-21T01:30:00Z', 11907435);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:57:55Z', 76, '2026-10-21T01:30:00Z', 11952630);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:58:05Z', 77, '2026-10-21T01:30:00Z', 11958358);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:58:55Z', 77, '2026-10-21T01:30:00Z', 12291131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:59:05Z', 78, '2026-10-21T01:30:00Z', 12294834);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-20T23:59:34Z', 78, '2026-10-21T01:30:00Z', 12299534);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:00:34Z', 78, '2026-10-21T01:30:00Z', 12320424);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:01:34Z', 78, '2026-10-21T01:30:00Z', 12321990);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:02:05Z', 79, '2026-10-21T01:30:00Z', 12332012);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:03:05Z', 79, '2026-10-21T01:30:00Z', 12341414);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:04:05Z', 79, '2026-10-21T01:30:00Z', 12346662);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:04:34Z', 79, '2026-10-21T01:30:00Z', 12356793);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:05:34Z', 79, '2026-10-21T01:30:00Z', 12364794);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:06:34Z', 79, '2026-10-21T01:30:00Z', 12378399);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:07:34Z', 79, '2026-10-21T01:30:00Z', 12396139);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:08:05Z', 80, '2026-10-21T01:30:00Z', 12401064);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:09:05Z', 80, '2026-10-21T01:30:00Z', 12418130);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:09:55Z', 80, '2026-10-21T01:30:00Z', 12547143);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:10:55Z', 81, '2026-10-21T01:30:00Z', 12565896);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:11:55Z', 81, '2026-10-21T01:30:00Z', 12590992);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:12:34Z', 81, '2026-10-21T01:30:00Z', 12598164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:12:42.7029808Z', 82, '2026-10-21T01:30:00Z', 12601575);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:12:42Z', 82, '2026-10-21T01:30:00Z', 12601575);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:15:34Z', 82, '2026-10-21T01:30:00Z', 12638171);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:16:35Z', 82, '2026-10-21T01:30:00Z', 12650725);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:17:27.7372807Z', 82, '2026-10-21T01:30:00Z', 12653005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:17:35Z', 82, '2026-10-21T01:30:00Z', 12653005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:18:34Z', 82, '2026-10-21T01:30:00Z', 12659914);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:19:34Z', 82, '2026-10-21T01:30:00Z', 12675018);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:20:34Z', 82, '2026-10-21T01:30:00Z', 12827535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:21:05Z', 83, '2026-10-21T01:30:00Z', 12827535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:22:05Z', 83, '2026-10-21T01:30:00Z', 12854972);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:23:05Z', 83, '2026-10-21T01:30:00Z', 12868335);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:23:32Z', 85, '2026-10-21T01:30:00Z', 13222604);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:24:34Z', 85, '2026-10-21T01:30:00Z', 13223697);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:25:34Z', 85, '2026-10-21T01:30:00Z', 13223697);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:26:34Z', 85, '2026-10-21T01:30:00Z', 13225854);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:27:34Z', 85, '2026-10-21T01:30:00Z', 13226475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:28:08Z', 86, '2026-10-21T01:30:00Z', 13653017);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:28:37Z', 87, '2026-10-21T01:30:00Z', 13667995);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:32:42Z', 87, '2026-10-21T01:30:00Z', 13674877);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:37:20Z', 87, '2026-10-21T01:30:00Z', 13676896);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:41:25Z', 88, '2026-10-21T01:30:00Z', 13707080);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:42:54Z', 88, '2026-10-21T01:30:00Z', 13720561);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:47:00Z', 88, '2026-10-21T01:30:00Z', 13724663);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:53:23Z', 88, '2026-10-21T01:30:00Z', 13726642);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T00:57:35Z', 89, '2026-10-21T01:30:00Z', 13807633);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:01:41Z', 89, '2026-10-21T01:30:00Z', 13886232);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:02:00Z', 89, '2026-10-21T01:30:00Z', 13888340);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:06:11Z', 89, '2026-10-21T01:30:00Z', 13989880);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:08:35Z', 90, '2026-10-21T01:30:00Z', 14084422);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:12:39Z', 91, '2026-10-21T01:30:00Z', 14186912);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:14:14Z', 91, '2026-10-21T01:30:00Z', 14260092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:18:19Z', 92, '2026-10-21T01:30:00Z', 14401112);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:22:33Z', 93, '2026-10-21T01:30:00Z', 14515660);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:25:12Z', 93, '2026-10-21T01:30:00Z', 14557889);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:29:19Z', 93, '2026-10-21T01:30:00Z', 14590783);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:30:10Z', 0, '2026-10-21T06:30:00Z', 1559);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:30:40Z', 0, '2026-10-21T06:30:00Z', 13198);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:31:06Z', 0, '2026-10-21T06:30:00Z', 23044);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:31:33Z', 0, '2026-10-21T06:30:00Z', 33234);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:32:06Z', 0, '2026-10-21T06:30:00Z', 43360);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:32:35Z', 0, '2026-10-21T06:30:00Z', 48957);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:33:06Z', 0, '2026-10-21T06:30:00Z', 58135);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:33:31.9683899Z', 0, '2026-10-21T06:30:00Z', 59300);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:33:35Z', 0, '2026-10-21T06:30:00Z', 59300);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:34:22Z', 0, '2026-10-21T06:30:00Z', 59300);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:34:35Z', 0, '2026-10-21T06:30:00Z', 59300);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:34:45Z', 1, '2026-10-21T06:30:00Z', 71158);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:35:02Z', 1, '2026-10-21T06:30:00Z', 86404);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:37:35Z', 2, '2026-10-21T06:30:00Z', 323294);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:38:05Z', 3, '2026-10-21T06:30:00Z', 590525);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:38:45Z', 4, '2026-10-21T06:30:00Z', 824666);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:39:51Z', 5, '2026-10-21T06:30:00Z', 1029443);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:41:09Z', 6, '2026-10-21T06:30:00Z', 1176097);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:42:35Z', 7, '2026-10-21T06:30:00Z', 1492362);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:42:57Z', 8, '2026-10-21T06:30:00Z', 1577053);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:43:37Z', 8, '2026-10-21T06:30:00Z', 1663142);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:44:47Z', 9, '2026-10-21T06:30:00Z', 1857180);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:45:41Z', 10, '2026-10-21T06:30:00Z', 1963962);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:48:48Z', 11, '2026-10-21T06:30:00Z', 2134295);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:49:22Z', 12, '2026-10-21T06:30:00Z', 2269663);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:50:06Z', 13, '2026-10-21T06:30:00Z', 2387712);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:50:13Z', 13, '2026-10-21T06:30:00Z', 2403316);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:50:55Z', 14, '2026-10-21T06:30:00Z', 2571551);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:52:33Z', 15, '2026-10-21T06:30:00Z', 2819402);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:52:58Z', 16, '2026-10-21T06:30:00Z', 2873202);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:54:14Z', 17, '2026-10-21T06:30:00Z', 2999209);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:56:07Z', 18, '2026-10-21T06:30:00Z', 3122485);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:57:45Z', 19, '2026-10-21T06:30:00Z', 3250365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:57:50Z', 19, '2026-10-21T06:30:00Z', 3250365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:58:23Z', 20, '2026-10-21T06:30:00Z', 3351290);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T01:59:54Z', 21, '2026-10-21T06:30:00Z', 3422810);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:02:21Z', 22, '2026-10-21T06:30:00Z', 3610218);

-- pace_ledger
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-09-17T07:10:00Z', NULL, '2026-09-17T02:10:00Z', 'Over', 'hit the 5-hour limit at 12:21 AM', NULL, NULL, 4387274, 0, 0, '2026-09-17T05:21:20.426Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-09-18T18:30:00Z', NULL, '2026-09-18T13:30:00Z', 'Over', 'hit the 5-hour limit at 1:24 PM', NULL, NULL, 2417996, 0, 0, '2026-09-18T18:24:22.627Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-09-19T01:20:00Z', NULL, '2026-09-18T20:20:00Z', 'Over', 'hit the 5-hour limit at 6:15 PM', NULL, NULL, 2010885, 0, 0, '2026-09-18T23:15:16.914Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-09-20T04:20:00Z', NULL, '2026-09-19T23:20:00Z', 'Over', 'hit the 5-hour limit at 8:25 PM', NULL, NULL, 1334554, 0, 0, '2026-09-20T01:25:47.297Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-09-20T12:10:00Z', NULL, '2026-09-20T07:10:00Z', 'Over', 'hit the 5-hour limit at 6:27 AM', NULL, NULL, 2293347, 0, 0, '2026-09-20T11:27:21.43Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-09-29T10:40:00Z', NULL, '2026-09-29T05:40:00Z', 'Over', 'hit the 5-hour limit at 5:23 AM', NULL, NULL, 2422671, 0, 0, '2026-09-29T10:23:47.024Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-05T08:10:00Z', NULL, '2026-10-05T03:10:00Z', 'Over', 'hit the 5-hour limit at 12:42 AM', NULL, NULL, 3835194, 0, 0, '2026-10-05T05:42:25.039Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-05T16:00:00Z', NULL, '2026-10-05T11:00:00Z', 'Over', 'hit the 5-hour limit at 7:58 AM', NULL, NULL, 2463297, 0, 0, '2026-10-05T12:58:07.606Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-06T06:50:00Z', NULL, '2026-10-06T01:50:00Z', 'Over', 'hit the 5-hour limit at 12:11 AM', NULL, NULL, 2898070, 0, 0, '2026-10-06T05:11:31.799Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-07T19:00:00Z', NULL, '2026-10-07T14:00:00Z', 'Over', 'hit the 5-hour limit at 1:54 PM', NULL, NULL, 2977001, 0, 0, '2026-10-07T18:54:16.834Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-08T03:50:00Z', NULL, '2026-10-07T22:50:00Z', 'Over', 'hit the 5-hour limit at 9:35 PM', NULL, NULL, 4911255, 0, 0, '2026-10-08T02:35:16.246Z', '2026-10-07T22:50:24.001Z', 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-08T09:30:00Z', NULL, '2026-10-08T04:30:00Z', 'Over', 'hit the 5-hour limit at 11:57 PM', NULL, NULL, 6113312, 0, 0, '2026-10-08T04:57:28.488Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-09T03:40:00Z', NULL, '2026-10-08T22:40:00Z', 'Over', 'hit the 5-hour limit at 6:09 PM', NULL, NULL, 2584552, 0, 0, '2026-10-08T23:09:08.008Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-11T11:10:00Z', NULL, '2026-10-11T06:10:00Z', 'Over', 'hit the 5-hour limit at 5:26 AM', NULL, NULL, 9249313, 0, 0, '2026-10-11T10:26:39.117Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-12T03:20:00Z', NULL, '2026-10-11T22:20:00Z', 'Over', 'hit the 5-hour limit at 9:23 PM', NULL, NULL, 10959964, 0, 0, '2026-10-12T02:23:59.813Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-13T05:10:00Z', NULL, '2026-10-13T00:10:00Z', 'Over', 'hit the 5-hour limit at 9:30 PM', NULL, NULL, 3291934, 0, 0, '2026-10-13T02:30:23.617Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-13T23:40:00Z', NULL, '2026-10-13T18:40:00Z', 'Over', 'hit the 5-hour limit at 4:10 PM', NULL, NULL, 11054417, 0, 0, '2026-10-13T21:10:04.485Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-14T04:40:00Z', NULL, '2026-10-13T23:40:00Z', 'Over', 'hit the 5-hour limit at 10:46 PM', NULL, NULL, 15444291, 0, 0, '2026-10-14T03:46:12.783Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-14T09:40:00Z', NULL, '2026-10-14T04:40:00Z', 'Over', 'hit the 5-hour limit at 1:23 AM', NULL, NULL, 9673640, 0, 0, '2026-10-14T06:23:56.449Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-14T19:00:00Z', NULL, '2026-10-14T14:00:00Z', 'Over', 'hit the 5-hour limit at 10:04 AM', NULL, NULL, 9476483, 0, 0, '2026-10-14T15:04:56.306Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-15T00:20:00Z', NULL, '2026-10-14T19:20:00Z', 'Over', 'hit the 5-hour limit at 4:30 PM', NULL, NULL, 11130839, 0, 0, '2026-10-14T21:30:47.132Z', NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-15T06:40:00Z', NULL, '2026-10-15T01:40:00Z', 'Blind', 'Claude''s meter unseen for 1h while 3.4M were spent', 9, 9, 4073440, 116, 0, NULL, NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-15T01:40:00Z","ToUtc":"2026-10-15T03:12:44Z","Fresh":3418875,"Length":"01:32:44"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-15T11:40:00Z', NULL, '2026-10-15T06:40:00Z', 'Over', 'hit the 5-hour limit at 6:19 AM', 80, 77, 10237894, 624, 0, '2026-10-15T11:19:56.351Z', NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-15T10:06:44Z","ToUtc":"2026-10-15T10:33:24Z","Fresh":1062040,"Length":"00:26:40"},{"FromUtc":"2026-10-15T10:49:42Z","ToUtc":"2026-10-15T11:06:26Z","Fresh":324500,"Length":"00:16:44"},{"FromUtc":"2026-10-15T11:06:44Z","ToUtc":"2026-10-15T11:38:58Z","Fresh":1812223,"Length":"00:32:14"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-15T16:50:00Z', NULL, '2026-10-15T11:50:00Z', 'Blind', 'Claude''s meter unseen for 3h while 7.3M were spent', 88, 88, 7561900, 32, 0, NULL, NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-15T11:50:00Z","ToUtc":"2026-10-15T15:48:33Z","Fresh":7257169,"Length":"03:58:33"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-15T21:50:00Z', NULL, '2026-10-15T16:50:00Z', 'Over', 'hit the 5-hour limit at 3:48 PM', 100, 100, 9081824, 312, 34.1, '2026-10-15T20:48:56.637Z', NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-15T17:02:26Z","ToUtc":"2026-10-15T17:23:59Z","Fresh":826956,"Length":"00:21:33"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-16T02:50:00Z', NULL, '2026-10-15T21:50:00Z', 'Under', 'ended at 88%', 88, 88, 9691770, 1191, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-16T07:50:00Z', NULL, '2026-10-16T02:50:00Z', 'Under', 'ended at 75%', 75, 75, 7490470, 1375, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-16T12:50:00Z', NULL, '2026-10-16T07:50:00Z', 'Under', 'ended at 11%', 11, 11, 1264257, 1238, 0, NULL, NULL, 218, 175.2, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-16T19:10:00Z', NULL, '2026-10-16T14:10:00Z', 'Under', 'ended at 94%', 94, 94, 9447994, 765, 0, NULL, NULL, 1934, 1074.5, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-17T00:10:00Z', NULL, '2026-10-16T19:10:00Z', 'Ok', 'ended at 96%', 96, 96, 12062105, 207, 0, NULL, NULL, 1361, 917.7, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-17T05:10:00Z', NULL, '2026-10-17T00:10:00Z', 'Over', 'hit the 5-hour limit at 11:58 PM', 100, 100, 13123631, 333, 14.8, '2026-10-17T04:58:28.293Z', NULL, 1723, 834.1, 5, 1, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-17T10:30:00Z', NULL, '2026-10-17T05:30:00Z', 'Under', 'ended at 91%', 91, 91, 13356319, 166, 0, NULL, NULL, 1387, 1554.6, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-17T15:30:00Z', NULL, '2026-10-17T10:30:00Z', 'Blind', 'Claude''s meter unseen for 2h while 3.8M were spent', 38, 38, 5673853, 28, 0, NULL, NULL, 302, 228.9, 0, 0, 0, '[{"FromUtc":"2026-10-17T10:37:24Z","ToUtc":"2026-10-17T11:22:33Z","Fresh":1988578,"Length":"00:45:09"},{"FromUtc":"2026-10-17T11:22:33Z","ToUtc":"2026-10-17T12:41:25Z","Fresh":814603,"Length":"01:18:52"},{"FromUtc":"2026-10-17T13:47:31Z","ToUtc":"2026-10-17T14:03:39Z","Fresh":476784,"Length":"00:16:08"},{"FromUtc":"2026-10-17T14:59:57Z","ToUtc":"2026-10-17T15:26:30Z","Fresh":535129,"Length":"00:26:33"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-17T20:30:00Z', NULL, '2026-10-17T15:30:00Z', 'Ok', 'ended at 96%', 96, 96, 11486902, 96, 0, NULL, NULL, 1450, 585.4, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-18T01:30:00Z', NULL, '2026-10-17T20:30:00Z', 'Under', 'ended at 87%', 87, 87, 10400934, 88, 0, NULL, NULL, 1247, 1058.5, 2, 23, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-18T11:10:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-18T06:10:00Z', 'Under', 'ended at 31%', 31, 31, 3102244, 56, 0, NULL, NULL, 184, 32.6, 0, 0, 5, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-18T20:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-18T15:20:00Z', 'Under', 'ended at 89%', 89, 89, 14957323, 101, 0, NULL, NULL, 639, 132.8, 4, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-19T01:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-18T20:20:00Z', 'Under', 'ended at 65%', 65, 65, 9803953, 83, 0, NULL, NULL, 327, 47.3, 4, 0, 2, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-19T06:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-19T01:20:00Z', 'Ok', 'ended at 95%', 95, 95, 12619093, 111, 0, NULL, NULL, 1737, 388.5, 42, 0, 2, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-19T11:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-19T06:20:00Z', 'Blind', 'Claude''s meter unseen for 1h while 3.5M were spent', 49, 49, 8210693, 73, 0, NULL, NULL, 3, 1.3, 0, 0, 0, '[{"FromUtc":"2026-10-19T06:29:58Z","ToUtc":"2026-10-19T07:05:04Z","Fresh":827245,"Length":"00:35:06"},{"FromUtc":"2026-10-19T07:15:42Z","ToUtc":"2026-10-19T07:34:48Z","Fresh":968303,"Length":"00:19:06"},{"FromUtc":"2026-10-19T07:37:42Z","ToUtc":"2026-10-19T07:52:44Z","Fresh":1126075,"Length":"00:15:02"},{"FromUtc":"2026-10-19T09:55:20Z","ToUtc":"2026-10-19T10:41:02Z","Fresh":563013,"Length":"00:45:42"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-19T19:30:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-19T14:30:00Z', 'Ok', 'ended at 96%', 96, 96, 13542326, 115, 0, NULL, NULL, 1575, 525.8, 15, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T00:30:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-19T19:30:00Z', 'Under', 'ended at 57%', 57, 57, 10132667, 194, 0, NULL, NULL, 868, 307.2, 13, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T03:20:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-19T22:20:00Z', 'Under', 'ended at 12%', 12, 12, 2155428, 13, 0, NULL, NULL, 728, 460.3, 36, 0, 4, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T03:50:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-19T22:50:00Z', 'Under', 'ended at 58%', 58, 58, 2705499, 44, 0, NULL, NULL, 812, 686.3, 39, 0, 4, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T05:50:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T00:50:00Z', 'Under', 'ended at 28%', 28, 28, 5292382, 32, 0, NULL, NULL, 769, 542.9, 33, 0, 4, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T07:30:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T02:30:00Z', 'Under', 'ended at 0%', 0, 0, 1692, 1, 0, NULL, NULL, 487, 488.3, 15, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T08:50:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T03:50:00Z', 'Under', 'ended at 53%', 53, 53, 11573529, 67, 0, NULL, NULL, 177, 51.7, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T15:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T10:30:00Z', 'Under', 'ended at 83%', 83, 83, 15109835, 202, 0, NULL, NULL, 17, 17, 3, 0, 3, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-20T20:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T15:30:00Z', 'Under', 'ended at 87%', 87, 87, 13112000, 109, 0, NULL, NULL, 99, 6.1, 4, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T01:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T20:30:00Z', 'Under', 'ended at 93%', 93, 93, 14590783, 275, 0, NULL, NULL, 718, 199.2, 14, 0, 8, '[]');

-- usage_history
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-07', 'GTFO.EZ', 'Claude', NULL, NULL, 3524458);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-07', 'MindAttic.Web', 'Claude', NULL, NULL, 419517);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-07', 'Prose', 'Claude', NULL, NULL, 15206960);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-08', 'KdpPublish', 'Claude', NULL, NULL, 20854);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-08', 'MindAttic.Export', 'Claude', NULL, NULL, 269996);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-08', 'MindAttic.Launcher', 'Claude', NULL, NULL, 2279642);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-08', 'MindAttic.Web', 'Claude', NULL, NULL, 182788);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-08', 'Prose', 'Claude', NULL, NULL, 10906344);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-08', 'SyntheticExchangeTheory', 'Claude', NULL, NULL, 8961067);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-09', '(workspace root)', 'Claude', NULL, NULL, 3148420);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-09', 'MindAttic.Launcher', 'Claude', NULL, NULL, 1466793);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-09', 'MindAttic.Web', 'Claude', NULL, NULL, 616311);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-09', 'Prose', 'Claude', NULL, NULL, 11591502);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-09', 'Prose', 'Codex', NULL, NULL, 21079);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-09', 'SyntheticExchangeTheory', 'Claude', NULL, NULL, 251680);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-10', '(workspace root)', 'Claude', NULL, NULL, 1936501);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-10', 'GTFO.EZ', 'Claude', NULL, NULL, 244811);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-10', 'MindAttic.Launcher', 'Claude', NULL, NULL, 2576627);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-10', 'Prose', 'Claude', NULL, NULL, 8174125);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', '(workspace root)', 'Claude', NULL, NULL, 646340);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'GTFO.EZ', 'Claude', NULL, NULL, 822392);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'GridGameCaravan', 'Claude', NULL, NULL, 994738);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'GridGameGaiden', 'Claude', NULL, NULL, 5576776);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'MindAttic.Browser', 'Claude', NULL, NULL, 36363);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'MindAttic.Launcher', 'Claude', NULL, NULL, 3711050);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'MindAttic.Web', 'Claude', NULL, NULL, 1404437);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'Prose', 'Claude', NULL, NULL, 6263426);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'UnityEve', 'Claude', NULL, NULL, 4718583);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'UnityRonin', 'Claude', NULL, NULL, 1170535);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-11', 'UnitySolarSystem', 'Claude', NULL, NULL, 6214924);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', '(workspace root)', 'Claude', NULL, NULL, 4571670);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', 'GridGameGaiden', 'Claude', NULL, NULL, 6748252);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', 'MindAttic.Capture', 'Claude', NULL, NULL, 1104983);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', 'MindAttic.Launcher', 'Claude', NULL, NULL, 1201269);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', 'MindAttic.Legion', 'Claude', NULL, NULL, 87627);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', 'Prose', 'Claude', NULL, NULL, 19300188);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-12', 'UnityEve', 'Claude', NULL, NULL, 2749438);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', '(workspace root)', 'Claude', NULL, NULL, 3674747);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'Gadfly', 'Claude', NULL, NULL, 760750);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'GridGameGaiden', 'Claude', NULL, NULL, 5176387);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Browser', 'Claude', NULL, NULL, 3450213);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Launcher', 'Claude', NULL, NULL, 2152001);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Legion', 'Claude', NULL, NULL, 1204623);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 2108888);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'Prose', 'Claude', NULL, NULL, 40224388);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'SyntheticExchangeTheory', 'Claude', NULL, NULL, 176265);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', '(workspace root)', 'Claude', NULL, NULL, 2168892);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'GridGameGaiden', 'Claude', NULL, NULL, 250974);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'MindAttic.Launcher', 'Claude', NULL, NULL, 767669);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'MindAttic.Log', 'Claude', NULL, NULL, 1312534);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 6151618);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'Prose', 'Claude', NULL, NULL, 33752980);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', '(outside workspace)', 'Claude', NULL, NULL, 495428);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', '(outside workspace)', 'Claude', NULL, '2026-10-15T21:00:00Z', 3155690);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', '(workspace root)', 'Claude', NULL, NULL, 2600362);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', '(workspace root)', 'Claude', NULL, '2026-10-15T21:00:00Z', 1061089);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'AutoWebNav', 'Claude', NULL, NULL, 135009);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Automata', 'Claude', NULL, NULL, 922326);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Cursory', 'Claude', NULL, NULL, 99804);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Formicarium', 'Claude', NULL, NULL, 144481);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GTFO.EZ', 'Claude', NULL, NULL, 22411);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GTFO.EZ', 'Claude', NULL, '2026-10-15T21:00:00Z', 2261322);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GTFO.SkipDrop', 'Claude', NULL, NULL, 249611);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Gadfly', 'Claude', NULL, NULL, 84653);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GridGameGaiden', 'Claude', NULL, NULL, 1348353);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GridGameGaiden', 'Claude', NULL, '2026-10-15T21:00:00Z', 6203848);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MediaButler', 'Claude', NULL, NULL, 258891);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Authentication', 'Claude', NULL, NULL, 246740);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Capture', 'Claude', NULL, NULL, 622827);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Deploy', 'Claude', NULL, NULL, 94337);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Ideas', 'Claude', NULL, NULL, 162693);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Launcher', 'Claude', NULL, NULL, 109561);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Legion', 'Claude', NULL, NULL, 64505);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Log', 'Claude', NULL, NULL, 259598);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Media', 'Claude', NULL, NULL, 58402);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 2438779);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Orchestrator', 'Claude', NULL, '2026-10-15T21:00:00Z', 3708727);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Psst', 'Claude', NULL, NULL, 45057);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Telemetry', 'Claude', NULL, NULL, 123716);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Vault', 'Claude', NULL, NULL, 44565);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'OpenCredentials', 'Claude', NULL, NULL, 133990);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Prose', 'Claude', NULL, NULL, 464417);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Prose', 'Claude', NULL, '2026-10-15T21:00:00Z', 12787973);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'TaxRateCollector', 'Claude', NULL, NULL, 111114);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'UnityRonin', 'Claude', NULL, NULL, 78248);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', '(workspace root)', 'Claude', NULL, NULL, 32489643);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GTFO.CrashTestDummies', 'Claude', NULL, NULL, 719758);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GTFO.EZ', 'Claude', NULL, NULL, 248521);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GTFO.SkipDrop', 'Claude', NULL, NULL, 131362);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GridGameGaiden', 'Claude', NULL, NULL, 1872729);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'MindAttic.Capture', 'Claude', NULL, NULL, 587628);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'MindAttic.Launcher', 'Claude', NULL, NULL, 452413);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 3254400);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'Prose', 'Claude', NULL, NULL, 5381163);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'Unstuck', 'Claude', NULL, NULL, 1445263);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', '(workspace root)', 'Claude', NULL, NULL, 19046894);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2164691);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.CrashTestDummies', 'Claude', NULL, NULL, 437119);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.EZ', 'Claude', NULL, NULL, 137002);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.EZ', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1422405);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.SkipDrop', 'Claude', NULL, NULL, 83140);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GridGameGaiden', 'Claude', NULL, NULL, 1697037);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GridGameGaiden', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 791549);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Browser', 'Claude', NULL, NULL, 612946);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Capture', 'Claude', NULL, NULL, 676337);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Launcher', 'Claude', NULL, NULL, 100032);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 16894404);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3210792);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Vault', 'Claude', NULL, NULL, 129138);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'Prose', 'Claude', NULL, NULL, 4275346);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'SyntheticExchangeTheory', 'Claude', NULL, NULL, 136464);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 14047778);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'GTFO.CrashTestDummies', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 683316);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'GTFO.EZ', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 6936509);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'GridGameGaiden', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 172681);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'MindAttic.Export', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 39539);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 34694995);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3381774);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 544435);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15405061);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', 'billed:api-key', NULL, 110945);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 319892);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1249890);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4283181);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ.Shared', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1692);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ.Shared', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 203113);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GridGameGaiden', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1982564);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GridGameGaiden', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 12981645);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'MindAttic.Orchestrator', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2096848);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2965008);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3482350);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'ProcessLogger', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 917453);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'ProcessLogger', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 105732);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'Prose', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 91497);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 689564);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'UnitySpace', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1608190);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'UnitySpace', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3172525);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 11755588);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'GTFO.EZ', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4807881);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'GridGameGaiden', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 6898739);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Launcher', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 186545);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Legion', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 284349);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 20937099);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Tickets', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 441127);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Unity.Multiplayer', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15360);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'UnitySpace', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7878551);

-- ticket_costs
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('010e2c71-f77b-493b-b09f-10a70efa0a5d', 'GG-T27', 'chore', 'GridGameGaiden', 91808, '2026-10-15T11:18:11.352Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('01da70bc-b97a-412f-9004-0f594715b508', 'GE-T10', 'feature', 'GTFO.EZ', 178991, '2026-10-15T09:15:50.031Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a05f65bc714b46d40', 'M.ORCH-T372', 'fix', 'MindAttic.Orchestrator', 88047, '2026-10-18T08:07:00.191Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a109bad33834a052d', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 470437, '2026-10-18T05:43:24.301Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a21a55039a64af479', 'M.ORCH-T386', 'fix', 'MindAttic.Orchestrator', 34034, '2026-10-18T08:03:30.589Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a2af32f568f74309f', 'M.ORCH-T374', 'chore', 'MindAttic.Orchestrator', 123082, '2026-10-18T08:01:30.247Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a2c7f3af2d5c64da1', 'M.ORCH-T320', 'fix', 'MindAttic.Orchestrator', 74251, '2026-10-18T05:59:52.013Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a300889420223522f', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 743399, '2026-10-18T07:25:03.324Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a308c21d98bd12182', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 458277, '2026-10-18T10:35:31.422Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a323b666d2c43d956', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 2370505, '2026-10-18T10:12:39.306Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a32e1293e3e11e9f8', 'M.ORCH-T389', 'fix', 'MindAttic.Orchestrator', 52383, '2026-10-18T08:23:05.289Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a39a10707569bf892', 'M.ORCH-T284', 'fix', 'MindAttic.Orchestrator', 64515, '2026-10-18T02:17:14.658Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a4237d1a9b122ab8d', 'M.ORCH-T385', 'fix', 'MindAttic.Orchestrator', 184725, '2026-10-18T08:07:27.665Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a549a4d2e99478bb9', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 266801, '2026-10-18T10:58:48.206Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a83b5bd29c0fb4308', 'M.ORCH-T384', 'fix', 'MindAttic.Orchestrator', 162632, '2026-10-18T08:15:39.15Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-aa85b48a671ad3c60', 'M.ORCH-T373', 'chore', 'MindAttic.Orchestrator', 60764, '2026-10-18T07:54:25.482Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-aaf74d2482caa969d', 'M.ORCH-T256', 'feature', 'MindAttic.Orchestrator', 929480, '2026-10-18T08:42:12.979Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-ab7bef213271a31a3', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 238536, '2026-10-18T07:51:21.476Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-ab9bcf96854c66e56', 'M.ORCH-T234', 'feature', 'MindAttic.Orchestrator', 491427, '2026-10-18T09:07:06.263Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-abf2ee2dfd56739d5', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 322044, '2026-10-18T11:28:56.044Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-ad5b6f9414ec8eb67', 'M.ORCH-T375', 'fix', 'MindAttic.Orchestrator', 201827, '2026-10-18T08:08:10.537Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-afa2c1c389bb61df3', 'M.ORCH-T388', 'fix', 'MindAttic.Orchestrator', 165698, '2026-10-18T08:31:30.981Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('02890e56-5257-49dd-8659-245102519bfa', 'GE-T3', 'chore', 'GTFO.EZ', 1749292, '2026-10-15T09:45:02.94Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('02cca644-9a1f-4424-813d-edfe444baa65', 'SS-T88', 'chore', 'Prose', 373434, '2026-10-15T20:48:59.345Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('030e5a5e-44f3-4b5a-a229-4d9707cf5903', 'GG-T21', 'chore', 'GridGameGaiden', 52863, '2026-10-15T11:13:00.014Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('034a58c5-8a10-4f41-9766-506a3cce90e4', 'USS-T39', 'feature', 'UnitySolarSystem', 290571, '2026-10-15T11:18:48.065Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('03ddf508-a736-4d57-a26c-3350b33e18b1', 'SS-T38', 'chore', 'Prose', 245228, '2026-10-15T12:54:52.624Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('053545c4-2407-4c70-b0d0-4f1a0533e4e8', 'GG-T18', 'feature', 'GridGameGaiden', 27421, '2026-10-15T13:12:27.381Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('05637a82-0d68-46c4-b77c-798a6a0c30f6', 'AUT-T11', 'chore', 'Automata', 62425, '2026-10-16T00:56:27.827Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0657de29-2ef7-498f-b99b-937d9deef28b', 'MAO-T84', 'feature', 'MindAttic.Orchestrator', 749664, '2026-10-15T08:18:31.745Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0687c7fd-a751-4729-a87c-00812f3d32e9', 'M.CAPT-T2', 'chore', 'MindAttic.Capture', 551651, '2026-10-16T02:05:24.47Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('090436c7-c731-48fd-8e1d-23070d467dca', 'U.RON-T2', 'chore', 'UnityRonin', 78248, '2026-10-16T01:30:25.95Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-a3cfe9c237caed6b0', 'G.EZ-T26', 'chore', 'GTFO.EZ', 404945, '2026-10-18T03:03:31.362Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-a8a88167f7ced5ec9', 'G.EZ-T28', 'chore', 'GTFO.EZ', 188662, '2026-10-18T02:52:41.9Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-ae14e91077f74649e', 'G.EZ-T27', 'chore', 'GTFO.EZ', 31506, '2026-10-18T02:49:02.782Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-af704ff01b2a3d501', 'G.EZ-T29', 'docs', 'GTFO.EZ', 107781, '2026-10-18T02:51:43.736Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0f0deb42-1b30-43f2-ad51-ee05e2037c8a', 'SS-T95', 'chore', 'Prose', 129365, '2026-10-15T18:18:26.471Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0f5d1472-92bc-4241-9d95-fa6f19cd2d87', 'SS-T57', 'chore', 'Prose', 81168, '2026-10-15T12:44:26.548Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('0ff1d016-3c88-48f4-ac85-a3673d5c6dec', 'M.AUTH-T3', 'chore', 'MindAttic.Authentication', 121409, '2026-10-16T01:52:29.507Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('107e3d68-dd6f-440e-a0ef-8ea144cf8f22', 'GGC-T3', 'chore', 'GridGameCaravan', 63008, '2026-10-16T01:40:45.331Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a084b5be806617811', 'M.ORCH-T186', 'feature', 'MindAttic.Orchestrator', 37273, '2026-10-17T23:51:20.384Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a15fc697d5d9ec701', 'M.ORCH-T252', 'fix', 'MindAttic.Orchestrator', 105581, '2026-10-17T21:07:06.621Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a1a31220cb5c1c36f', 'M.ORCH-T178', 'fix', 'MindAttic.Orchestrator', 28272, '2026-10-17T16:07:18.954Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a1ce20af14ce2d27c', 'M.ORCH-T203', 'fix', 'MindAttic.Orchestrator', 114317, '2026-10-17T18:14:55.192Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a3c2a4fbc44fcd359', 'M.ORCH-T177', 'fix', 'MindAttic.Orchestrator', 15995, '2026-10-17T16:01:01.69Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a3e9fc2f2321b7f5a', 'M.ORCH-T174', 'chore', 'MindAttic.Orchestrator', 127590, '2026-10-18T00:23:11.976Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a42e976dd83f31c39', 'M.ORCH-T205', 'fix', 'MindAttic.Orchestrator', 196931, '2026-10-17T18:57:34.002Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a4b335b19ee13aea6', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 4102394, '2026-10-18T01:12:54.612Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a4c867dfc5367236e', 'M.ORCH-T255', 'fix', 'MindAttic.Orchestrator', 157502, '2026-10-17T23:39:15.816Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a5e4abfe2d14e5ef5', 'M.ORCH-T176', 'feature', 'MindAttic.Orchestrator', 109831, '2026-10-18T00:02:38.295Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a63ba8790d45af02d', 'M.ORCH-T196', 'fix', 'MindAttic.Orchestrator', 139906, '2026-10-17T16:34:18.844Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a700e8b0517ec706e', 'M.ORCH-T200', 'fix', 'MindAttic.Orchestrator', 178702, '2026-10-17T16:46:08.453Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a72b90568585bf074', 'M.ORCH-T175', 'docs', 'MindAttic.Orchestrator', 86016, '2026-10-17T16:03:33.029Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a7bdd8f57daba8ae6', 'M.ORCH-T189', 'chore', 'MindAttic.Orchestrator', 31642, '2026-10-18T00:21:04.369Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a83974bdc86d254e9', 'M.ORCH-T225', 'fix', 'MindAttic.Orchestrator', 58853, '2026-10-17T18:43:28.27Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a83f02ae65316f5e3', 'M.ORCH-T254', 'fix', 'MindAttic.Orchestrator', 87779, '2026-10-17T22:46:14.611Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a95c791d67d1160d3', 'M.ORCH-T251', 'fix', 'MindAttic.Orchestrator', 65938, '2026-10-17T20:36:45.891Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aa8a7b4eb40d1989f', 'M.ORCH-T245', 'fix', 'MindAttic.Orchestrator', 301950, '2026-10-17T21:08:57.239Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aa967033ebff88f58', 'M.ORCH-T152', 'chore', 'MindAttic.Orchestrator', 50289, '2026-10-17T16:12:46.816Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aba9e38f249215e18', 'M.ORCH-T179', 'fix', 'MindAttic.Orchestrator', 81634, '2026-10-17T16:09:16.629Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-abb3046df73b9bfbd', 'M.ORCH-T194', 'fix', 'MindAttic.Orchestrator', 32422, '2026-10-17T16:18:51.135Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ac4b0ec5da035c58c', 'M.ORCH-T201', 'fix', 'MindAttic.Orchestrator', 49020, '2026-10-17T17:06:45.009Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ac5f335204b8becd8', 'M.ORCH-T188', 'fix', 'MindAttic.Orchestrator', 63750, '2026-10-17T16:21:50.378Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ac69d59c4460c404e', 'M.ORCH-T173', 'chore', 'MindAttic.Orchestrator', 15047, '2026-10-17T23:57:47.498Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ada16f0b6497c485c', 'M.ORCH-T250', 'fix', 'MindAttic.Orchestrator', 36089, '2026-10-17T20:34:26.462Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aeb44daf4a03b8e28', 'M.ORCH-T151', 'chore', 'MindAttic.Orchestrator', 50057, '2026-10-17T16:01:43.181Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aeb804e16a7670798', 'M.ORCH-T185', 'chore', 'MindAttic.Orchestrator', 46060, '2026-10-18T00:24:54.534Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-af8772854cdf2bc38', 'M.ORCH-T253', 'fix', 'MindAttic.Orchestrator', 41996, '2026-10-17T20:36:12.474Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-af9f9027a9b08bdd9', 'M.ORCH-T198', 'feature', 'MindAttic.Orchestrator', 157077, '2026-10-17T15:53:41.744Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('13cb820a-3dc3-4951-a193-e21d77771fe8', 'SS-T14', 'chore', 'Prose', 32733, '2026-10-15T11:58:04.131Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('140574bc-26c8-4d4d-92e1-c0012e600219', 'CUR-T2', 'chore', 'Cursory', 45531, '2026-10-16T00:54:58.889Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('15cf5108-874e-4e5e-91a5-366c125f93cb', 'PRS-T16', 'fix', 'Prose', 425165, '2026-10-16T07:27:38.953Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('160bb34c-06aa-4d20-ae84-967580cf3179', 'SS-T17', 'chore', 'Prose', 317446, '2026-10-15T12:49:09.975Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('17c76877-4c52-4335-b7d0-0e516a026985', 'GG-T50', 'feature', 'GridGameGaiden', 261741, '2026-10-15T16:19:18.202Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1836d17d-b4aa-44ce-9a05-806e4c289e7e', 'PRS-T15', 'fix', 'Prose', 214629, '2026-10-16T00:56:27.24Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('18646bb1-e071-4e3c-8f1d-d803c1ee7d8b', 'SS-T68', 'chore', 'Prose', 230431, '2026-10-15T18:39:44.203Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('193fa3a0-18d0-473a-b49e-683b2af22292', 'GG-T42', 'feature', 'GridGameGaiden', 225349, '2026-10-15T14:26:40.89Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1c7e3212-f7ba-4793-b876-327d14fd62a0', 'GG-T26', 'chore', 'GridGameGaiden', 84129, '2026-10-15T11:16:42.051Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-a475fe354cbc07f2b', 'M.ORCH-T473', 'fix', 'MindAttic.Orchestrator', 230004, '2026-10-19T19:52:56.097Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-a4e7c135868582316', 'M.ORCH-T471', 'feature', 'MindAttic.Orchestrator', 168953, '2026-10-19T18:02:41.672Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-a7cba2e3ec747c4d3', 'M.ORCH-T399', 'chore', 'MindAttic.Orchestrator', 58705, '2026-10-19T19:39:28.907Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-ab49d12287567f712', 'M.ORCH-T467', 'fix', 'MindAttic.Orchestrator', 101604, '2026-10-19T19:44:14.558Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-ab516d5ba2e550fdb', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 1282018, '2026-10-19T20:09:59.107Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-ac8b6f92cdf0bdc40', 'M.ORCH-T470', 'fix', 'MindAttic.Orchestrator', 68404, '2026-10-19T19:42:06.789Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1d244528-2ebd-45e8-9e03-7c2da10caee3', 'GG-T26', 'chore', 'GridGameGaiden', 84416, '2026-10-16T01:25:03.593Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1d4dae76-55db-4041-b32a-7d0b3435b12f', 'GG-T38', 'feature', 'GridGameGaiden', 272031, '2026-10-15T13:47:12.042Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a19d7f9d073754d46', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 4128000, '2026-10-16T21:52:49.488Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a3ceaa950aae0e1eb', 'U.SPC-T50', 'chore', 'UnitySolarSystem', 14988, '2026-10-16T15:11:18.367Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a6369f6642cdca902', 'U.SPC-T52', 'docs', 'UnitySolarSystem', 140912, '2026-10-16T15:43:37.089Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a6f031fce3c085f38', 'U.SPC-T43', 'feature', 'UnitySolarSystem', 292184, '2026-10-16T16:08:44.817Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-ac700f62253add5d6', 'U.SPC-T54', 'feature', 'UnitySolarSystem', 462041, '2026-10-16T16:17:53.768Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-af6e752745078ff2e', 'U.SPC-T51', 'chore', 'UnitySolarSystem', 263122, '2026-10-16T16:02:14.355Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-af7fbfa4f3db5b308', 'U.SPC-T44', 'feature', 'UnitySolarSystem', 224994, '2026-10-16T15:56:44.605Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('1fb53b01-f937-4979-ad11-192d797f2fb3', 'M.AUTH-T2', 'chore', 'MindAttic.Authentication', 125331, '2026-10-16T01:02:19.297Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('203e7bea-da94-44c3-b7a0-da1f03358235', 'M.PSST-T2', 'chore', 'MindAttic.Psst', 45057, '2026-10-16T01:05:46.838Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('23c96b9d-6918-4855-81df-44992e462d4c', 'SS-T25', 'chore', 'Prose', 19465, '2026-10-15T11:56:34.07Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('24c789dc-69cb-49bf-974c-1ec48da0b50d', 'GG-T37', 'feature', 'GridGameGaiden', 89563, '2026-10-15T13:37:25.28Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('27fe7ce7-120f-4946-9130-122a3879b678', 'U.SPC-T41', 'feature', 'UnitySolarSystem', 159769, '2026-10-15T11:19:43.96Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('290a401f-e0f6-4678-aac3-361ea19a5492', 'GG-T45', 'feature', 'GridGameGaiden', 326932, '2026-10-15T15:32:45.457Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2a07085f-b3c3-4394-b658-44fd1d331a05', 'M.VALT-T2', 'chore', 'MindAttic.Vault', 44565, '2026-10-16T01:07:57.963Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2bb4867e-31ed-479b-9b9a-426c662b86e6', 'GG-T30', 'chore', 'GridGameGaiden', 139930, '2026-10-15T11:19:44.723Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2c51efb7-7fba-4b21-8c6f-9cc9c2ac59a2', 'GG-T46', 'feature', 'GridGameGaiden', 385490, '2026-10-15T15:20:35.827Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2cd2e503-c388-43a5-a723-89d9354d9aee', 'USS-T44', 'feature', 'UnitySolarSystem', 255951, '2026-10-15T11:19:51.119Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2efe68bb-8df4-4797-a605-fc49205bbc52', 'GG-T39', 'feature', 'GridGameGaiden', 437324, '2026-10-15T14:22:03.227Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2f1aced6-85ec-4a7f-ac09-7168b62b2aa5', 'U.SPC-T33', 'chore', 'UnitySolarSystem', 184785, '2026-10-15T10:55:30.855Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('2f869b18-505e-4dd4-baa4-dd182fc97114', 'AUT-T7', 'chore', 'Automata', 124342, '2026-10-16T00:28:11.312Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('30a9bdc9-e00e-42c1-9728-ab2496765886', 'SS-T84', 'chore', 'Prose', 884833, '2026-10-15T20:34:37.505Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('320acbe9-0043-4941-be4d-2741433f8e7d', 'GG-T41', 'feature', 'GridGameGaiden', 326932, '2026-10-15T15:05:18.842Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('326b33a7-da2b-426a-9e1a-6eb790cacacf', 'SS-T90', 'chore', 'Prose', 194445, '2026-10-15T18:50:12.639Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3465b5a1-50cc-4ba4-9d42-5520d2a103f1', 'SS-T66', 'chore', 'Prose', 70344, '2026-10-15T16:47:35.709Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('34f48634-9016-42f5-9d89-f55f1e1d9548', 'U.SPC-T35', 'chore', 'UnitySolarSystem', 290819, '2026-10-15T11:06:09.699Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('34f7fc98-c887-49de-a7fe-a56dceb2ac23', 'MAI-T3', 'chore', 'MindAttic.Ideas', 162693, '2026-10-16T01:51:34.055Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('35741c8f-f8ad-43d1-83c0-3b750e46d397', 'M.LNCH-T21', 'fix', 'MindAttic.Launcher', 109561, '2026-10-16T00:32:53.119Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a238312c4e5ffc775', 'U.SPC-T317', 'fix', 'UnitySpace', 195104, '2026-10-20T03:52:21.946Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a3720cd08da9d5ccd', 'U.SPC-T320', 'fix', 'UnitySpace', 114009, '2026-10-20T03:08:04.864Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a44a2859b46863bbd', 'U.SPC-T319', 'fix', 'UnitySpace', 149143, '2026-10-20T03:52:49.629Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a50d9d7aa1a8ac467', 'U.SPC-T321', 'fix', 'UnitySpace', 210848, '2026-10-20T03:52:19.223Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a73d86ed921766dfa', 'U.SPC-T49', 'chore', 'UnitySpace', 266767, '2026-10-20T03:53:01.276Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a752314e74a417631', 'U.SPC-T322', 'fix', 'UnitySpace', 137666, '2026-10-20T03:52:59.822Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a7817391489e0aa19', 'U.SPC-T318', 'fix', 'UnitySpace', 177550, '2026-10-20T03:52:26.149Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-aed248f922b41911c', 'U.SPC-T316', 'fix', 'UnitySpace', 216662, '2026-10-20T03:50:32.947Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37981662-0d80-4a46-8b6c-0fa9ac3908a4', 'AUT-T10', 'chore', 'Automata', 71430, '2026-10-16T00:55:42.197Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('37d38e1f-baab-4d7a-b1d8-e48fd9c9ca23', 'LOG-T3', 'chore', 'MindAttic.Log', 184183, '2026-10-16T02:01:31.335Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3a2ba785-a6d2-4d7c-8caa-befa2e1e1818', 'SS-T63', 'chore', 'Prose', 71935, '2026-10-15T17:14:47.601Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d206b98-1dc7-4d8b-be76-36ee9806c41d/agent-a82ff2123ff9b16b5', 'PL-T15', 'docs', 'ProcessLogger', 216510, '2026-10-20T00:21:51.418Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d206b98-1dc7-4d8b-be76-36ee9806c41d/agent-aab7ed261f4aa74ca', 'PL-T12', 'chore', 'ProcessLogger', 267096, '2026-10-20T00:54:14.631Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a0d8ce001efc0c9db', 'M.ORCH-T128', 'chore', 'MindAttic.Orchestrator', 49795, '2026-10-16T15:18:48.278Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a4cea79899ba7a725', 'M.ORCH-T127', 'chore', 'MindAttic.Orchestrator', 51166, '2026-10-16T15:14:42.971Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a6d913c62e00362e7', 'M.ORCH-T126', 'chore', 'MindAttic.Orchestrator', 66558, '2026-10-16T15:23:33.133Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a751a1fc3487a6bc7', 'M.ORCH-T132', 'chore', 'MindAttic.Orchestrator', 44033, '2026-10-16T15:25:12.214Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-ace5de353eb4fd0a1', 'M.ORCH-T129', 'chore', 'MindAttic.Orchestrator', 33059, '2026-10-16T15:10:43.781Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-ad2e0a4db0fd59194', 'M.ORCH-T130', 'chore', 'MindAttic.Orchestrator', 70219, '2026-10-16T15:35:26.257Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-aed374a90ad1c162e', 'M.ORCH-T131', 'chore', 'MindAttic.Orchestrator', 87154, '2026-10-16T15:43:39.032Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-a45e552c0f832690d', 'M.FLOW-T62', 'docs', 'MindAttic.Workflow', 136403, '2026-10-19T02:31:17.525Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-a46f0190026730abb', 'M.FLOW-T56', 'fix', 'MindAttic.Workflow', 122651, '2026-10-19T02:17:58.772Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-a4f227a852a735f8d', 'M.FLOW-T59', 'chore', 'MindAttic.Workflow', 857161, '2026-10-19T04:44:20.904Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-aa16a79874bed75f0', 'M.FLOW-T95', 'fix', 'MindAttic.Workflow', 72918, '2026-10-19T02:36:38.224Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-ad31ea860c2c791bf', 'M.FLOW-T58', 'fix', 'MindAttic.Workflow', 243773, '2026-10-19T02:27:05.998Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-afddfa587628e67ba', 'M.FLOW-T51', 'fix', 'MindAttic.Workflow', 164817, '2026-10-19T02:20:26.023Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('401c6587-2762-44a7-8561-08643f6925f0', 'SS-T65', 'chore', 'Prose', 175333, '2026-10-15T17:26:04.955Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('44c614df-9ad6-4700-80e8-041f58238120', 'SS-T87', 'chore', 'Prose', 676481, '2026-10-15T20:48:53.883Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('4c3b85b1-1314-4760-8c2d-dc7646297287', 'GG-T18', 'feature', 'GridGameGaiden', 51661, '2026-10-15T09:03:05.17Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('4e31197a-bbdf-45da-9680-649daa08074b', 'GG-T36', 'feature', 'GridGameGaiden', 493758, '2026-10-15T14:55:05.913Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('50b04d70-5d44-456c-91cd-ef5fe59673d4', 'GG-T40', 'feature', 'GridGameGaiden', 199068, '2026-10-15T14:08:41.696Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('52728cb7-3bd9-4584-bd3c-85b160ecb8fc', 'MB-T2', 'chore', 'MediaButler', 89617, '2026-10-16T00:57:42.857Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0ba12577fa8e45dc', 'M.COMP-T38', 'feature', 'MindAttic.Orchestrator', 127618, '2026-10-20T16:39:34.278Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0d42cfb2a06475a7', 'M.ORCH-T529', 'feature', 'MindAttic.Orchestrator', 435180, '2026-10-20T16:29:43.21Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0e3d919b3a5943fa', 'M.COMP-T40', 'fix', 'MindAttic.Orchestrator', 61821, '2026-10-20T16:35:54.797Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0f84cbc3c47f3062', 'M.ORCH-T498', 'feature', 'MindAttic.Orchestrator', 303584, '2026-10-20T16:37:37.972Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a19f81cedda9a64df', 'M.ORCH-T531', 'feature', 'MindAttic.Orchestrator', 479681, '2026-10-20T17:19:26.346Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a20a26aed61bbdf9e', 'M.ORCH-T502', 'fix', 'MindAttic.Orchestrator', 158766, '2026-10-20T10:42:47.461Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a26193165d7117e73', 'M.COMP-T37', 'feature', 'MindAttic.Orchestrator', 140102, '2026-10-20T16:40:17.405Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2b76bb8af5b62e8f', 'M.COMP-T4', 'feature', 'MindAttic.Orchestrator', 118161, '2026-10-20T17:36:06.36Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2b90151de4c3863e', 'M.ORCH-T528', 'feature', 'MindAttic.Orchestrator', 2090587, '2026-10-20T17:23:13.756Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2cd508c9e0427428', 'M.ORCH-T493', 'feature', 'MindAttic.Orchestrator', 89172, '2026-10-20T13:54:41.762Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2d518f9fe6ff25de', 'M.ORCH-T523', 'fix', 'MindAttic.Orchestrator', 296292, '2026-10-20T13:06:30.802Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a39b229ffbba9a8e5', 'M.ORCH-T524', 'feature', 'MindAttic.Orchestrator', 789753, '2026-10-20T14:36:09.751Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a3fb971ffe8f2f47e', 'M.COMP-T12', 'feature', 'MindAttic.Orchestrator', 151487, '2026-10-20T16:23:33.071Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a430fc71ce4411b0f', 'M.ORCH-T498', 'feature', 'MindAttic.Orchestrator', 30351, '2026-10-20T04:24:09.084Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a43a48eb87b11f082', 'M.ORCH-T525', 'fix', 'MindAttic.Orchestrator', 74217, '2026-10-20T13:34:21.633Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a457b5c1ffa0a2f88', 'M.COMP-T34', 'feature', 'MindAttic.Orchestrator', 106285, '2026-10-20T16:37:41.581Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a464d6b0ca2b01a25', 'M.ORCH-T496', 'fix', 'MindAttic.Orchestrator', 73702, '2026-10-20T04:30:17.589Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a5ab31c6cb3776a50', 'M.COMP-T10', 'docs', 'MindAttic.Orchestrator', 64348, '2026-10-20T15:57:30.01Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a5e3bf02493eb12e0', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 2087498, '2026-10-20T11:59:22.884Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a64f9bfabb9ab6a6f', 'M.ORCH-T500', 'feature', 'MindAttic.Orchestrator', 295353, '2026-10-20T04:40:01.934Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a660804f3b7fd3d50', 'M.ORCH-T492', 'feature', 'MindAttic.Orchestrator', 388036, '2026-10-20T13:41:11.251Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a6935387cda2ec8ca', 'M.ORCH-T494', 'chore', 'MindAttic.Orchestrator', 954085, '2026-10-20T14:07:08.604Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a6af75f799b6888a9', 'M.COMP-T36', 'fix', 'MindAttic.Orchestrator', 40873, '2026-10-20T16:18:19.969Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a6d98bb62bf797436', 'M.ORCH-T499', 'feature', 'MindAttic.Orchestrator', 248751, '2026-10-20T16:16:29.137Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a75a0d96a7e02eb09', 'M.ORCH-T182', 'chore', 'MindAttic.Orchestrator', 106608, '2026-10-20T17:41:14.182Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a75e220ba5346cd10', 'M.COMP-T30', 'feature', 'MindAttic.Orchestrator', 163133, '2026-10-20T16:24:21.238Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a7b97de0e8dfea5b8', 'M.COMP-T32', 'feature', 'MindAttic.Orchestrator', 80707, '2026-10-20T16:20:25.464Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a7cfe17aeceaf179b', 'M.ORCH-T501', 'feature', 'MindAttic.Orchestrator', 300838, '2026-10-20T04:54:48.255Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a8282d634083be933', 'M.ORCH-T558', 'feature', 'MindAttic.Orchestrator', 254273, '2026-10-21T00:12:36.677Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a88e1db7d3fce96f8', 'M.COMP-T7', 'chore', 'MindAttic.Orchestrator', 357008, '2026-10-20T16:14:10.969Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a8af468aaa5235af7', 'M.COMP-T33', 'feature', 'MindAttic.Orchestrator', 75288, '2026-10-20T17:02:41.02Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a908577035ef64e91', 'M.ORCH-T181', 'chore', 'MindAttic.Orchestrator', 405457, '2026-10-20T18:02:59.698Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a98dc4b914b0e5ff3', 'M.ORCH-T497', 'fix', 'MindAttic.Orchestrator', 140629, '2026-10-20T04:37:33.183Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a99ec9a1cc49b31a6', 'M.COMP-T29', 'fix', 'MindAttic.Orchestrator', 34527, '2026-10-20T16:14:36.664Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a9a5a2476da7e1eb1', 'M.ORCH-T522', 'feature', 'MindAttic.Orchestrator', 208385, '2026-10-20T13:14:37.541Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa05a0b7041b1d2bb', 'M.COMP-T39', 'fix', 'MindAttic.Orchestrator', 74002, '2026-10-20T16:35:20.466Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa582eb0a887d80c3', 'M.COMP-T31', 'feature', 'MindAttic.Orchestrator', 108326, '2026-10-20T16:22:45.147Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa86d28e439ee3cbb', 'M.ORCH-T495', 'fix', 'MindAttic.Orchestrator', 389981, '2026-10-20T04:43:59.15Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa8937aa055a4ad82', 'M.COMP-T11', 'chore', 'MindAttic.Orchestrator', 157347, '2026-10-20T17:38:22.036Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-ab2843c7ad2d8dbc1', 'M.ORCH-T527', 'feature', 'MindAttic.Orchestrator', 241614, '2026-10-20T15:27:23.383Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-abb2d704e194dfa18', 'M.COMP-T28', 'fix', 'MindAttic.Orchestrator', 42183, '2026-10-20T16:15:24.821Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-abdd3222004aae1f1', 'M.COMP-T8', 'chore', 'MindAttic.Orchestrator', 35524, '2026-10-20T15:57:12.759Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-abeaee46dbf709d0b', 'M.COMP-T9', 'chore', 'MindAttic.Orchestrator', 176064, '2026-10-20T16:12:36.109Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-adcb6fabe9f6665b6', 'M.COMP-T35', 'feature', 'MindAttic.Orchestrator', 73156, '2026-10-20T16:36:58.098Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-adf19216e2400429c', 'M.ORCH-T532', 'fix', 'MindAttic.Orchestrator', 369744, '2026-10-20T16:59:45.332Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-ae5aee90badb46820', 'M.ORCH-T526', 'feature', 'MindAttic.Orchestrator', 908034, '2026-10-20T15:11:20.618Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aee4e29ec4e1b2f0a', 'M.ORCH-T490', 'feature', 'MindAttic.Orchestrator', 397963, '2026-10-20T13:21:37.056Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aefd185e8a3bb8c7e', 'M.COMP-T15', 'feature', 'MindAttic.Orchestrator', 134633, '2026-10-20T15:55:02.57Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-af40bf46a49b0dda9', 'M.ORCH-T530', 'fix', 'MindAttic.Orchestrator', 175125, '2026-10-20T16:32:11.755Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('57c77d99-8c11-482d-a104-1f0fd0912870', 'TRC-T2', 'chore', 'TaxRateCollector', 111114, '2026-10-16T01:19:27.667Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5ad89c14-79aa-4653-8793-95a0890762e7', 'SS-T19', 'chore', 'Prose', 36900, '2026-10-15T11:56:21.312Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5e352c10-e91b-475f-86a1-fdd011baed35', 'MAO-T79', 'feature', 'MindAttic.Orchestrator', 5921453, '2026-10-15T09:11:58.222Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('5e95c216-37bf-4ec9-abea-6ae6d3d089fd', 'MB-T3', 'chore', 'MediaButler', 169274, '2026-10-16T01:45:49.201Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('630533bf-8238-4561-b8fb-7093eef2a447', 'SS-T67', 'chore', 'Prose', 533913, '2026-10-15T18:08:08.638Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a030a11dcbbf068b1', 'U.SPC-T262', 'chore', 'UnitySolarSystem', 47513, '2026-10-19T02:46:37.963Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a2371e4296b9363dd', 'U.SPC-T90', 'feature', 'UnitySolarSystem', 750543, '2026-10-19T07:04:22.96Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a477c6cf2b63727e9', 'U.SPC-T290', 'fix', 'UnitySolarSystem', 370115, '2026-10-19T07:26:32.402Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a666bf147279aaf7c', 'U.SPC-T97', 'feature', 'UnitySolarSystem', 741423, '2026-10-19T08:17:16.635Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a6abf9500711e74b0', 'U.SPC-T94', 'feature', 'UnitySolarSystem', 1480912, '2026-10-19T08:42:24.345Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a738e4f4089d6e051', 'U.SPC-T103', 'feature', 'UnitySolarSystem', 594598, '2026-10-19T08:07:28.14Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a7adeace36a592783', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 2729267, '2026-10-19T06:12:28.188Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a82feb35f83fc0899', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 350148, '2026-10-19T15:22:05.821Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a9d0a2171fd89d059', 'U.SPC-T98', 'feature', 'UnitySolarSystem', 1058700, '2026-10-19T08:03:58.523Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-aa26767fe4e15f2a9', 'U.SPC-T91', 'feature', 'UnitySolarSystem', 580045, '2026-10-19T07:34:40.703Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-aa4cd1264d95a961b', 'U.SPC-T294', 'fix', 'UnitySolarSystem', 118533, '2026-10-19T14:54:37.848Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-aab18138ceaed8ccd', 'U.SPC-T92', 'feature', 'UnitySolarSystem', 1117815, '2026-10-19T08:26:21.271Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-ab509cfba9db0b702', 'U.SPC-T104', 'feature', 'UnitySolarSystem', 463474, '2026-10-19T03:47:05.879Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-ae63ec997f3bded11', 'U.SPC-T291', 'fix', 'UnitySolarSystem', 112528, '2026-10-19T07:47:26.832Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-ae8bde54c45d452c8', 'U.SPC-T93', 'feature', 'UnitySolarSystem', 438876, '2026-10-19T15:05:09.117Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-af00ae1ce1feca030', 'U.SPC-T293', 'fix', 'UnitySolarSystem', 722334, '2026-10-19T08:50:11.91Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6889017a-4db9-468d-b7d4-c9e74da50582', 'U.SPC-T40', 'feature', 'UnitySolarSystem', 160642, '2026-10-15T11:21:34.158Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a521cf2a262b93920', 'M.FLOW-T116', 'fix', 'MindAttic.Workflow', 119027, '2026-10-20T12:53:56.4Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a5d092fcc8805b2c5', 'M.FLOW-T63', 'feature', 'MindAttic.Workflow', 91537, '2026-10-20T04:26:56.423Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a70b837f8248aa123', 'M.FLOW-T61', 'chore', 'MindAttic.Workflow', 139805, '2026-10-20T04:29:46.822Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a93603d764b18d87d', 'M.FLOW-T96', 'fix', 'MindAttic.Workflow', 47899, '2026-10-20T04:25:20Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-ab60294a40cd9d5d8', 'M.FLOW-T60', 'chore', 'MindAttic.Workflow', 14115, '2026-10-20T04:23:20.293Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-ac6326db75e5b5f68', 'M.FLOW-T117', 'feature', 'MindAttic.Workflow', 96405, '2026-10-20T12:52:18.295Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a248aff33f9a78940', 'U.SPC-T67', 'feature', 'UnitySolarSystem', 394005, '2026-10-17T03:52:56.514Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a2b545b96756abf2b', 'U.SPC-T59', 'feature', 'UnitySolarSystem', 311132, '2026-10-17T00:08:12.489Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a2ff1120dcf4740bc', 'U.SPC-T101', 'feature', 'UnitySolarSystem', 1039085, '2026-10-17T10:06:17.998Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a31c46f81e988e100', 'U.SPC-T104', 'feature', 'UnitySolarSystem', 74207, '2026-10-17T04:54:04.286Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a3f00bc7b6f3cbe37', 'U.SPC-T40', 'feature', 'UnitySolarSystem', 209147, '2026-10-17T13:47:14.976Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a41601f3b1a1b6e8e', 'U.SPC-T58', 'feature', 'UnitySolarSystem', 360990, '2026-10-17T00:27:18.326Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a4e48e57239a39781', 'U.SPC-T96', 'feature', 'UnitySolarSystem', 1915674, '2026-10-17T09:10:59.647Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a5cdb652d446481d6', 'U.SPC-T100', 'feature', 'UnitySolarSystem', 602908, '2026-10-17T09:37:51.971Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a76fb1ef36ffec922', 'U.SPC-T95', 'feature', 'UnitySolarSystem', 2493712, '2026-10-17T05:39:01.129Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a913d0f8bfa261775', 'U.SPC-T57', 'feature', 'UnitySolarSystem', 582025, '2026-10-17T00:16:46.102Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-aa25fbd0fea58430f', 'U.SPC-T41', 'feature', 'UnitySolarSystem', 685454, '2026-10-17T14:03:34.648Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ab1481e4bfd4f9d17', 'U.SPC-T65', 'feature', 'UnitySolarSystem', 325573, '2026-10-17T02:17:05.132Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ab87b89b554c5e5b9', 'U.SPC-T42', 'feature', 'UnitySolarSystem', 236870, '2026-10-17T14:22:15.16Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-aba751382a6b59cdd', 'U.SPC-T61', 'feature', 'UnitySolarSystem', 410142, '2026-10-17T01:23:43.682Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-abc1f19080ddf9728', 'U.SPC-T102', 'feature', 'UnitySolarSystem', 1058338, '2026-10-17T10:17:50.618Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-abe0ed789b40f45a8', 'U.SPC-T182', 'fix', 'UnitySolarSystem', 523056, '2026-10-17T14:59:49.212Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ac1cb2b5b860b4396', 'U.SPC-T178', 'fix', 'UnitySolarSystem', 235109, '2026-10-17T06:05:09.555Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ac29b76500799739c', 'U.SPC-T62', 'feature', 'UnitySolarSystem', 1417093, '2026-10-17T04:01:23.757Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-acaaefff52a93b7a2', 'U.SPC-T45', 'feature', 'UnitySolarSystem', 326875, '2026-10-16T23:10:06.047Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ad7b971363bd201af', 'U.SPC-T79', 'fix', 'UnitySolarSystem', 48996, '2026-10-16T22:36:23.69Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-adbcc8d01720481f8', 'U.SPC-T56', 'feature', 'UnitySolarSystem', 283973, '2026-10-16T23:45:25.41Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ae84c9a48ce37a677', 'U.SPC-T66', 'feature', 'UnitySolarSystem', 304719, '2026-10-17T02:54:55.938Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-aecffc8f2ea4d13ad', 'U.SPC-T60', 'feature', 'UnitySolarSystem', 885678, '2026-10-17T01:50:12.709Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af11268c85885b0e5', 'U.SPC-T64', 'feature', 'UnitySolarSystem', 332052, '2026-10-17T01:34:05.265Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af3207b5845435ec5', 'U.SPC-T63', 'feature', 'UnitySolarSystem', 1316944, '2026-10-17T08:13:54.732Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af48d353f97a9d18c', 'U.SPC-T55', 'feature', 'UnitySolarSystem', 389307, '2026-10-16T23:21:04.795Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af8687d63aed431cf', 'U.SPC-T99', 'feature', 'UnitySolarSystem', 2680350, '2026-10-17T08:29:06.38Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af9fb1dda2b9b430a', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 2753723, '2026-10-17T13:29:39.304Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6c05b7c4-7cbe-4503-bed2-2a1b28f2e17d/agent-a1c8110db31e47875', 'PRS-T136', 'docs', 'Prose', 113862, '2026-10-19T20:07:26.984Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6d72b6f5-bc49-4578-ba97-2e3d3444af03', 'M.FLOW-T14', 'chore', 'MindAttic.Workflow', 231677, '2026-10-15T07:26:15.21Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a2471a89b9e63fcd0', 'GG-T93', 'feature', 'GridGameGaiden', 688743, '2026-10-19T16:30:14.633Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a30b3ba3072f7dc95', 'GG-T96', 'feature', 'GridGameGaiden', 344208, '2026-10-19T16:42:03.04Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a327f267b6975d224', 'GG-T95', 'feature', 'GridGameGaiden', 749888, '2026-10-19T17:08:55.31Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a339380d04380ad31', 'GG-T112', 'fix', 'GridGameGaiden', 113703, '2026-10-19T20:42:44.184Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a35e7ff5b3b9ce064', 'GG-T100', 'fix', 'GridGameGaiden', 471087, '2026-10-19T18:34:12.04Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a4a0b93e9dfe32a74', 'GG-T72', 'chore', 'GridGameGaiden', 125673, '2026-10-19T15:59:18.475Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a508ceafe251e42b8', 'GG-T121', 'fix', 'GridGameGaiden', 816239, '2026-10-19T21:32:45.226Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a54055289a7fcbb5b', 'GG-T73', 'docs', 'GridGameGaiden', 100785, '2026-10-19T15:18:39.638Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a67ae412e6356d228', 'GG-T97', 'fix', 'GridGameGaiden', 832791, '2026-10-19T19:28:07.007Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a716dd0490796b7cb', 'GG-T84', 'fix', 'GridGameGaiden', 128833, '2026-10-19T15:45:07.126Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a8a1613d6f571ec83', 'GG-T87', 'fix', 'GridGameGaiden', 166274, '2026-10-19T16:09:21.555Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aa53c02aec03f1431', 'GG-T70', 'chore', 'GridGameGaiden', 469118, '2026-10-19T16:50:37.97Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aa67d4518225c45e9', 'GG-T118', 'feature', 'GridGameGaiden', 1065049, '2026-10-19T21:44:21.372Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aae07a364b856b900', 'GG-T117', 'feature', 'GridGameGaiden', 284238, '2026-10-19T20:14:38.639Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ab77059f349a4d543', 'GG-T114', 'fix', 'GridGameGaiden', 256816, '2026-10-19T20:52:23.374Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ab837b7bdd5cc65c6', 'MAU-T2', 'feature', 'GridGameGaiden', 72635, '2026-10-19T20:32:10.647Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aca72e0e79147d144', 'GG-T98', 'fix', 'GridGameGaiden', 258853, '2026-10-19T17:36:53.57Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ad2d52dcf85c75880', 'GG-T119', 'feature', 'GridGameGaiden', 2211974, '2026-10-19T22:15:22.668Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ad49332e77f654c6d', 'GG-T110', 'fix', 'GridGameGaiden', 769664, '2026-10-19T19:19:08.033Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ad58acb77ce3d9adc', 'GG-T126', 'fix', 'GridGameGaiden', 563129, '2026-10-19T20:28:07.785Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-af180004d9fe18d58', 'GG-T129', 'fix', 'GridGameGaiden', 604165, '2026-10-19T21:09:13.906Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('6e4994f9-ce65-4ccf-8d32-0dc48241925d', 'GG-T43', 'feature', 'GridGameGaiden', 430422, '2026-10-15T14:46:49.253Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a0caa61c9dfc286f1', 'G.AWPN-T187', 'fix', 'GTFO.EZ', 21868, '2026-10-20T13:24:11.576Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a199fabc16ea03384', 'GAM-T41', 'feature', 'GTFO.EZ', 40935, '2026-10-20T04:24:34.686Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a1bec0b133536e40e', 'GAI-T14', 'fix', 'GTFO.EZ', 36786, '2026-10-20T04:24:29.227Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a25f79a47c184b03b', 'G.SD-T4', 'chore', 'GTFO.EZ', 16230, '2026-10-20T04:23:17.802Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a26b59b6f5af81025', 'G.AWPN-T188', 'fix', 'GTFO.EZ', 29593, '2026-10-20T13:25:17.693Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a2d5eac1cdaeeec2d', 'G.CTD-T4', 'chore', 'GTFO.EZ', 15844, '2026-10-20T04:22:45.478Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a329025e8f0fbd9fc', 'GAI-T15', 'fix', 'GTFO.EZ', 43633, '2026-10-20T04:24:42.334Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a3e6bcf2658530e63', 'G.SD-T5', 'chore', 'GTFO.EZ', 36657, '2026-10-20T04:24:41.304Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a5da9ef7206e599fb', 'G.AWPN-T8', 'chore', 'GTFO.EZ', 424670, '2026-10-20T13:56:32.524Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a7376f2f0bb2f4bf3', 'G.AWPN-T10', 'chore', 'GTFO.EZ', 131690, '2026-10-20T13:01:10.396Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aa11e74e2f7492d88', 'G.AWPN-T210', 'fix', 'GTFO.EZ', 38285, '2026-10-20T13:51:00.315Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ab36b110de08b01d9', 'G.CTD-T6', 'docs', 'GTFO.EZ', 61134, '2026-10-20T04:24:00.52Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ab7b8f385c3cc22db', 'G.CTD-T3', 'chore', 'GTFO.EZ', 114337, '2026-10-20T04:27:09.129Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ab82ebbc97a3b858e', 'G.CTD-T5', 'chore', 'GTFO.EZ', 89542, '2026-10-20T04:25:17.668Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ac2a22bc5753c1da3', 'G.AWPN-T189', 'fix', 'GTFO.EZ', 47764, '2026-10-20T13:26:11.305Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aca711b737d5fd90e', 'G.AWPN-T209', 'fix', 'GTFO.EZ', 38325, '2026-10-20T13:40:50.413Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-acb06516c6c4e7ab2', 'G.AWPN-T9', 'chore', 'GTFO.EZ', 16943, '2026-10-20T12:54:11.5Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ad157af0a5e1db80c', 'G.AWPN-T89', 'chore', 'GTFO.EZ', 22768, '2026-10-20T12:54:14.763Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ad9fb7d354011067d', 'G.AWPN-T208', 'fix', 'GTFO.EZ', 39930, '2026-10-20T13:41:01.757Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aeb834d6f88b1c5ed', 'G.SD-T6', 'docs', 'GTFO.EZ', 47794, '2026-10-20T04:23:56.116Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aecbbc0cd280dc8b7', 'G.SD-T3', 'chore', 'GTFO.EZ', 39441, '2026-10-20T04:23:44.951Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-af0cdc82875af8d06', 'G.AWPN-T186', 'fix', 'GTFO.EZ', 45089, '2026-10-20T12:55:39.177Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a15e39cfaf1725d03', 'UM-T6', 'feature', '(workspace root)', 147765, '2026-10-20T18:33:27.506Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a1a3046942864ec88', 'UC-T5', 'feature', '(workspace root)', 231704, '2026-10-20T20:49:40.485Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a1a4300f935be8b5d', 'UC-T3', 'chore', '(workspace root)', 130823, '2026-10-20T19:46:00.344Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a1fbee5b56bf0cee5', 'UM-T12', 'feature', '(workspace root)', 168913, '2026-10-20T20:39:36.268Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a21acede47ca29b9c', 'UM-T9', 'feature', '(workspace root)', 308157, '2026-10-20T19:24:14.358Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a3d16eb0826776aa8', 'UM-T15', 'feature', '(workspace root)', 1036348, '2026-10-20T22:52:07.156Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a516930353fd2c89e', 'UM-T7', 'feature', '(workspace root)', 143825, '2026-10-20T18:50:18.916Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a52e76a4f59a06c00', 'UM-T14', 'feature', '(workspace root)', 382448, '2026-10-20T21:20:57.655Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a59c230ff3be02374', 'UM-T23', 'feature', '(workspace root)', 402875, '2026-10-21T00:42:17.02Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a6af54e56c3e7a249', 'UC-T6', 'feature', '(workspace root)', 167432, '2026-10-20T20:58:20.276Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a8dd1a0a9175a15fd', 'UM-T11', 'feature', '(workspace root)', 268809, '2026-10-20T19:52:06Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a9850620f4fce731d', 'UM-T3', 'chore', '(workspace root)', 85488, '2026-10-20T17:51:48.473Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a9b304cc95621d45d', 'UM-T5', 'feature', '(workspace root)', 112937, '2026-10-20T18:20:30.049Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-aac7d8be8748297d7', 'UM-T10', 'feature', '(workspace root)', 318321, '2026-10-20T19:39:06.96Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-ab33f3b06943e0c25', 'UM-T4', 'feature', '(workspace root)', 157589, '2026-10-20T18:14:47.88Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-ace7fcd44179e4444', 'UM-T34', 'fix', '(workspace root)', 117527, '2026-10-20T21:26:47.468Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-ae58fc2124cc06628', 'UM-T8', 'feature', '(workspace root)', 259015, '2026-10-20T19:09:07.122Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-af132a04ecee6322b', 'UM-T16', 'feature', '(workspace root)', 372648, '2026-10-21T00:23:24.838Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-afae78455dda9332f', 'UC-T4', 'feature', '(workspace root)', 146078, '2026-10-20T20:36:34.306Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-aff229900c5f15281', 'UM-T13', 'feature', '(workspace root)', 347207, '2026-10-20T20:59:26.226Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('727c3e91-894d-40e1-80ee-8c4a123538f3', 'SS-T85', 'chore', 'Prose', 739134, '2026-10-15T20:48:34.45Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('72d2c115-e155-4990-9252-bfd323471271', 'U.SPC-T34', 'chore', 'UnitySolarSystem', 120179, '2026-10-15T10:53:46.527Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('73e45d1b-b1ba-4183-8bc8-ad138e00ac90', 'GG-T55', 'feature', 'GridGameGaiden', 245815, '2026-10-15T15:49:44.573Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('76d54fd4-c346-437c-936d-f61f475d1d9c', 'GG-T44', 'feature', 'GridGameGaiden', 233632, '2026-10-15T15:14:17.606Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('777043ff-2435-4c23-b5eb-f9dc26712a00', 'U.SPC-T31', 'chore', 'UnitySolarSystem', 147687, '2026-10-15T10:15:29.572Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('7838b3d2-863c-414f-95e7-eaf3bb4b4ba0', 'SS-T77', 'chore', 'Prose', 1555326, '2026-10-15T20:48:04.151Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a0245fc7c2cf689aa', 'GG-T143', 'fix', 'GridGameGaiden', 75273, '2026-10-20T05:48:19.731Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a2ec44edd6170fc81', 'GG-T120', 'feature', 'GridGameGaiden', 690075, '2026-10-20T05:14:48.319Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a3ca4141fd935b73e', 'GG-T138', 'fix', 'GridGameGaiden', 342024, '2026-10-20T06:31:30.117Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a3d0d6b1e86d7a6d3', 'GG-T150', 'fix', 'GridGameGaiden', 50213, '2026-10-20T05:49:30.172Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a531b6464191877c5', 'GG-T140', 'fix', 'GridGameGaiden', 239844, '2026-10-20T06:12:16.41Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a611d9ad195ab10a1', 'GG-T154', 'fix', 'GridGameGaiden', 263150, '2026-10-20T06:51:47.115Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a761ba80c550c981e', 'GG-T148', 'fix', 'GridGameGaiden', 108272, '2026-10-20T06:13:01.934Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a7fb3f7ad428d7c8f', 'GG-T145', 'fix', 'GridGameGaiden', 149446, '2026-10-20T06:23:08.578Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a8488d9057f7953c3', 'GG-T141', 'fix', 'GridGameGaiden', 185637, '2026-10-20T06:15:42.321Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a8bdab32ffc666122', 'GG-T149', 'fix', 'GridGameGaiden', 138494, '2026-10-20T06:15:34.653Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a92520fef841c48f7', 'GG-T142', 'fix', 'GridGameGaiden', 102586, '2026-10-20T05:57:48.136Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a97a4fe041eca3e97', 'GG-T144', 'fix', 'GridGameGaiden', 65828, '2026-10-20T05:49:13.89Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a97fcde6c0512bff1', 'GG-T115', 'docs', 'GridGameGaiden', 289717, '2026-10-20T06:07:05.69Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a98c4a46d923e440e', 'GG-T139', 'fix', 'GridGameGaiden', 186364, '2026-10-20T06:18:56.951Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a998d27faa3c38c1d', 'GG-T146', 'fix', 'GridGameGaiden', 382311, '2026-10-20T06:31:27.904Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a9a09476d6b733fd7', 'GG-T70', 'chore', 'GridGameGaiden', 3182079, '2026-10-20T07:10:10.446Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-aa37526fd7738cc1b', 'GG-T147', 'fix', 'GridGameGaiden', 156215, '2026-10-20T06:10:29.75Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-ac1914442c92d85e3', 'GG-T130', 'fix', 'GridGameGaiden', 251147, '2026-10-20T04:59:21.448Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-acc6be0aedda2f7ba', 'GG-T153', 'fix', 'GridGameGaiden', 200982, '2026-10-20T06:40:42.971Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-ae3b5713f94fc8a88', 'GG-T152', 'fix', 'GridGameGaiden', 166842, '2026-10-20T06:36:03.819Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('790ec0a7-a1db-4a71-b081-41e870622f4a', 'AWN-T3', 'chore', 'AutoWebNav', 66189, '2026-10-16T01:29:21.189Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('796d8450-d5f1-42cb-ad1f-03135e9f34dc', 'SS-T61', 'chore', 'Prose', 254662, '2026-10-15T18:28:47.551Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('7cd5ae65-619e-4bfd-a54f-9827e19b1f7d', 'GG-T25', 'chore', 'GridGameGaiden', 67707, '2026-10-16T01:28:57.456Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('7db50265-d3af-4335-bd88-75027f391494', 'SS-T94', 'chore', 'Prose', 237342, '2026-10-15T18:49:33.087Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('81477b29-986f-4f88-8bc7-ac56739357e8', 'SS-T89', 'chore', 'Prose', 404902, '2026-10-15T20:48:50.114Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('84815464-d977-4c85-8ce7-a7d07487de62', 'AUT-T5', 'chore', 'Automata', 155659, '2026-10-16T00:26:44.597Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a01d53b333e844d05', 'U.SPC-T323', 'fix', 'GridGameGaiden', 279846, '2026-10-21T01:59:13.724Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a106f9baeb644f360', 'GG-T158', 'chore', 'GridGameGaiden', 130917, '2026-10-21T01:58:10.102Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a168ad008e1d0bbd2', 'U.EVE-T20', 'chore', 'GridGameGaiden', 200103, '2026-10-21T02:01:06.541Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a4d63e1c6a967691a', 'UC-T16', 'chore', 'GridGameGaiden', 122543, '2026-10-21T01:50:11.614Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a5ab8b285fac374aa', 'U.EVE-T15', 'chore', 'GridGameGaiden', 31421, '2026-10-21T01:38:33.054Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a5f5da228131771aa', 'UM-T17', 'chore', 'GridGameGaiden', 215266, '2026-10-21T01:52:41.762Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a694ed3406d6cdbc2', 'U.SPC-T317', 'fix', 'GridGameGaiden', 71241, '2026-10-21T01:54:31.21Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a6d053950e81b554f', 'U.EVE-T16', 'chore', 'GridGameGaiden', 166030, '2026-10-21T02:02:06.691Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a8101f8beb944b357', 'U.SPC-T361', 'chore', 'GridGameGaiden', 257605, '2026-10-21T02:04:24.228Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a8bc6920dfb3b543c', 'UM-T35', 'chore', 'GridGameGaiden', 199211, '2026-10-21T01:58:15.205Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-aa83e1bbe25c5dc8d', 'U.SPC-T319', 'fix', 'GridGameGaiden', 65122, '2026-10-21T01:55:20.603Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-ab3cfa10b91b48cd4', 'U.SPC-T315', 'fix', 'GridGameGaiden', 187839, '2026-10-21T02:02:18.862Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-acc0b60034f32dda0', 'UM-T25', 'feature', 'GridGameGaiden', 121540, '2026-10-21T01:49:56.764Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-ad68d021771b08b06', 'U.SPC-T318', 'fix', 'GridGameGaiden', 52058, '2026-10-21T01:51:28.435Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-af670f0876d59303b', 'U.EVE-T14', 'chore', 'GridGameGaiden', 388627, '2026-10-21T02:00:24.241Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-afe149dd0addea669', 'U.EVE-T17', 'docs', 'GridGameGaiden', 232249, '2026-10-21T01:53:33.876Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('88f96354-7241-47a3-8646-380771e83f0a', 'SS-T18', 'chore', 'Prose', 166432, '2026-10-15T12:32:34.246Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('8a18e6e5-e63b-4728-9b6b-8f01429d6410', 'M.ORCH-T50', 'feature', 'MindAttic.Orchestrator', 255839, '2026-10-15T07:40:28.95Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('8c775618-bedb-40ce-a12d-9d5991cf8791', 'USS-T18', 'feature', 'UnitySolarSystem', 111374, '2026-10-15T10:34:39.901Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9066c252-4e61-4adc-8e1b-61120f513162', 'SS-T56', 'chore', 'Prose', 309300, '2026-10-15T13:05:39.825Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9173831a-1a8b-45e5-ab65-3105b64e8e81', 'GG-T28', 'chore', 'GridGameGaiden', 96220, '2026-10-15T11:11:55.835Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('933b404e-17e7-4c76-9234-ad8509611d86', 'GG-T29', 'chore', 'GridGameGaiden', 50328, '2026-10-15T11:18:54.85Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('934a0da6-dfdb-47b7-8c59-5df1ed5b172b', 'SS-T60', 'chore', 'Prose', 296593, '2026-10-15T18:09:02.789Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9510051b-bedf-48e0-ae56-8e23c87f025a', 'GG-T24', 'chore', 'GridGameGaiden', 33700, '2026-10-15T11:17:14.741Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a0417ae7865716731', 'M.ORCH-T197', 'fix', 'MindAttic.Orchestrator', 64106, '2026-10-17T06:14:42.115Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a06bab6a91dea397c', 'M.ORCH-T141', 'chore', 'MindAttic.Orchestrator', 49474, '2026-10-16T23:31:41.076Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a08a41884cab08542', 'M.ORCH-T138', 'chore', 'MindAttic.Orchestrator', 49846, '2026-10-16T23:22:13.781Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a0baaf30507b0bced', 'M.ORCH-T139', 'chore', 'MindAttic.Orchestrator', 81705, '2026-10-16T23:35:52.843Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a20862cdc043b49cb', 'M.ORCH-T142', 'chore', 'MindAttic.Orchestrator', 64921, '2026-10-16T23:35:29.625Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a4ad3fcc75b3811bf', 'M.ORCH-T136', 'chore', 'MindAttic.Orchestrator', 64849, '2026-10-16T23:16:25.57Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a639fe4283ef0434e', 'M.ORCH-T149', 'chore', 'MindAttic.Orchestrator', 86340, '2026-10-17T06:25:42.758Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a64eb29f5c2e0e88d', 'M.ORCH-T144', 'chore', 'MindAttic.Orchestrator', 52587, '2026-10-16T23:39:29.746Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a68bf9cc4d2df9353', 'M.ORCH-T143', 'chore', 'MindAttic.Orchestrator', 53137, '2026-10-16T23:31:56.71Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a7de5ecb7a7a45fa3', 'M.ORCH-T140', 'chore', 'MindAttic.Orchestrator', 64132, '2026-10-16T23:26:59.804Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a85c2069e907b2970', 'M.ORCH-T147', 'chore', 'MindAttic.Orchestrator', 61247, '2026-10-17T06:23:10.975Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a9589336f482fddbc', 'M.ORCH-T193', 'feature', 'MindAttic.Orchestrator', 47914, '2026-10-17T01:29:42.013Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a985bf105c8c4461c', 'M.ORCH-T192', 'feature', 'MindAttic.Orchestrator', 109041, '2026-10-17T02:10:29.649Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-aa99613b0518466a1', 'M.ORCH-T145', 'chore', 'MindAttic.Orchestrator', 44162, '2026-10-17T01:41:49.368Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-aacde50a5b3f401e7', 'M.ORCH-T195', 'fix', 'MindAttic.Orchestrator', 63799, '2026-10-17T06:11:23.855Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ab78bb9ac57a5254b', 'M.ORCH-T191', 'feature', 'MindAttic.Orchestrator', 58239, '2026-10-17T01:36:23.028Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-aba8977041db59693', 'M.ORCH-T134', 'chore', 'MindAttic.Orchestrator', 55061, '2026-10-16T22:40:48.946Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-abdc50678cb7e2874', 'M.ORCH-T150', 'chore', 'MindAttic.Orchestrator', 63534, '2026-10-17T06:23:31.636Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-acc2c3568d229e82d', 'M.ORCH-T137', 'chore', 'MindAttic.Orchestrator', 27467, '2026-10-16T23:13:52.828Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-acf372ab95018b87e', 'M.ORCH-T148', 'chore', 'MindAttic.Orchestrator', 68440, '2026-10-17T06:15:48.899Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ad03d8f347a487b3d', 'M.ORCH-T146', 'chore', 'MindAttic.Orchestrator', 39485, '2026-10-17T06:06:04.882Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ad8bb722efe86c1d9', 'M.ORCH-T135', 'chore', 'MindAttic.Orchestrator', 87137, '2026-10-16T22:53:31.144Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ae1255311c006be29', 'M.ORCH-T133', 'chore', 'MindAttic.Orchestrator', 86968, '2026-10-16T22:36:12.796Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9a851808-0c2d-48ed-8e81-75b916af6fb1', 'SS-T22', 'chore', 'Prose', 36102, '2026-10-15T11:56:51.712Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9bd28781-420c-4c16-b9bf-b4b7b470ed37', 'U.SPC-T46', 'feature', 'UnitySolarSystem', 191690, '2026-10-15T11:19:49.684Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9d42b472-3333-40e8-8d9f-d137053afb51', 'SS-T35', 'chore', 'Prose', 28013, '2026-10-15T11:58:00.618Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9eb76350-c323-4ffc-ab29-14cdc310dab9', 'GAD-T2', 'chore', 'Gadfly', 37325, '2026-10-16T00:54:09.924Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9f20f70a-158d-4a63-a196-bf7551e7790e', 'M.ORCH-T88', 'feature', 'MindAttic.Orchestrator', 102698, '2026-10-15T08:28:26.22Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('9fe45815-a62c-4adf-9387-4012a7bbbc50', 'GG-T25', 'chore', 'GridGameGaiden', 85779, '2026-10-15T11:17:20.874Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a0117a5f-7f1b-4b57-bb34-f1802ac5acdb', 'SS-T26', 'chore', 'Prose', 229926, '2026-10-15T12:43:15.261Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a08981a5-ebf6-4047-8e82-65210ac55dc6', 'GG-T30', 'chore', 'GridGameGaiden', 139899, '2026-10-16T01:44:08.446Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a161be04-0cf5-4976-a312-431ff54cfc4d', 'GG-T28', 'chore', 'GridGameGaiden', 116043, '2026-10-16T01:47:39.865Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a3a3a74a-e986-4c10-ab99-820001ddf0a3', 'M.DPLY-T3', 'chore', 'MindAttic.Deploy', 94337, '2026-10-16T01:53:16.766Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a0fea105d6b586e9b', 'U.SPC-T307', 'fix', 'UnitySpace', 44775, '2026-10-20T21:54:08.07Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a369a8fe23f6cf85c', 'U.SPC-T310', 'fix', 'UnitySpace', 517071, '2026-10-20T23:02:20.22Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a4e7a6ea4dab154df', 'U.SPC-T308', 'fix', 'UnitySpace', 160378, '2026-10-20T22:27:09.036Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a4f11e367409118bc', 'U.SPC-T321', 'fix', 'UnitySpace', 274608, '2026-10-20T22:41:44.694Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a620689ad861a2be3', 'U.SPC-T309', 'fix', 'UnitySpace', 183590, '2026-10-20T22:38:08.072Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a7bf7abcd6c5a424a', 'U.SPC-T306', 'fix', 'UnitySpace', 51656, '2026-10-20T21:56:05.572Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a9f479731441f8485', 'U.SPC-T312', 'fix', 'UnitySpace', 207203, '2026-10-20T22:24:24.729Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-aa203cc7f022350ba', 'U.SPC-T313', 'fix', 'UnitySpace', 226768, '2026-10-20T22:37:28.764Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-aacef402c12e66b0e', 'U.SPC-T311', 'fix', 'UnitySpace', 258883, '2026-10-20T22:40:13.268Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-abe50d9947774f852', 'U.SPC-T314', 'fix', 'UnitySpace', 180956, '2026-10-20T23:00:19.281Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-ac63f95d0583a51a6', 'U.SPC-T303', 'fix', 'UnitySpace', 45798, '2026-10-20T21:54:30.235Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-ac6a8066653293ca9', 'U.SPC-T49', 'chore', 'UnitySpace', 1644302, '2026-10-21T00:29:50.956Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-acd0b5cd00a73274a', 'U.SPC-T302', 'fix', 'UnitySpace', 224914, '2026-10-20T22:49:08.326Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-acede121a2caf942c', 'U.SPC-T304', 'fix', 'UnitySpace', 446433, '2026-10-20T22:51:09.959Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-aed48f8824ac2fb56', 'U.SPC-T301', 'fix', 'UnitySpace', 67783, '2026-10-20T21:52:59.211Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a07467bf8de6f1997', 'M.ORCH-T477', 'feature', 'MindAttic.Orchestrator', 900947, '2026-10-19T23:13:06.761Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a08f4e96b74685759', 'M.ORCH-T486', 'feature', 'MindAttic.Orchestrator', 568009, '2026-10-19T23:48:21.335Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a19f8d17e2fafb3e4', 'M.ORCH-T481', 'feature', 'MindAttic.Orchestrator', 1343192, '2026-10-19T23:11:35.372Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a45c098a04674ef41', 'M.ORCH-T475', 'chore', 'MindAttic.Orchestrator', 178449, '2026-10-19T22:41:31.852Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a699a227d34ee6ebc', 'M.ORCH-T482', 'fix', 'MindAttic.Orchestrator', 48128, '2026-10-19T22:34:37.195Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a7ee4a41ea30883f6', 'M.ORCH-T483', 'feature', 'MindAttic.Orchestrator', 266027, '2026-10-19T22:43:46.27Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-aa266a8ff05f8d788', 'M.ORCH-T485', 'fix', 'MindAttic.Orchestrator', 245844, '2026-10-19T22:50:52.675Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-ab41e989c253aafe0', 'M.ORCH-T468', 'fix', 'MindAttic.Orchestrator', 29978, '2026-10-19T22:35:24.678Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-ab7eccfef6edeb7c3', 'M.ORCH-T489', 'fix', 'MindAttic.Orchestrator', 240876, '2026-10-19T23:22:01.348Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-af274d2704d8ef5f4', 'M.ORCH-T490', 'feature', 'MindAttic.Orchestrator', 581844, '2026-10-20T01:34:30.336Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-afa4414d300ad131f', 'M.ORCH-T479', 'fix', 'MindAttic.Orchestrator', 315599, '2026-10-19T22:44:32.323Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('a860eb4f-87f7-48ca-b32d-5a51b75d369e', 'USS-T17', 'feature', 'UnitySolarSystem', 138831, '2026-10-15T10:27:07Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a074538cdff78e942', 'M.WD-T2', 'feature', 'MindAttic.WatchDog', 66577, '2026-10-19T02:08:48.926Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a16cda2cb1ea50393', 'M.WD-T3', 'chore', 'MindAttic.WatchDog', 68603, '2026-10-19T02:08:10.077Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a199b1e5a48505a2f', 'M.WD-T6', 'feature', 'MindAttic.WatchDog', 34539, '2026-10-19T15:16:45.02Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a434370eb192fa4cf', 'M.WD-T4', 'feature', 'MindAttic.WatchDog', 49580, '2026-10-19T02:38:08.537Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a59a09d3ce301a960', 'M.WD-T8', 'feature', 'MindAttic.WatchDog', 52807, '2026-10-19T15:20:18.951Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a9b005b09f85def97', 'M.WD-T9', 'feature', 'MindAttic.WatchDog', 44021, '2026-10-19T15:28:46.753Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-ab44faf67d61420a8', 'M.WD-T5', 'feature', 'MindAttic.WatchDog', 99563, '2026-10-19T15:26:43.45Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-ac5677315729a0c5b', 'M.WD-T7', 'feature', 'MindAttic.WatchDog', 59775, '2026-10-19T15:24:02.337Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-acd6527bdf8072ee6', 'M.WD-T11', 'feature', 'MindAttic.WatchDog', 55854, '2026-10-19T15:33:10.397Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-adf8a533c97c1713f', 'MAT-T4', 'docs', 'MindAttic.WatchDog', 41165, '2026-10-19T02:32:38.655Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('aa706789-f355-4bd6-9a29-cc6d8a53c560', 'AUT-T4', 'chore', 'Automata', 135488, '2026-10-16T00:25:47.242Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('ac01f4f8-fd13-438f-ba25-1c90451e79d9', 'GG-T47', 'feature', 'GridGameGaiden', 336822, '2026-10-15T15:49:46.277Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('ad10f6e2-debc-48b7-831a-4930f19d2f33', 'M.LEGN-T3', 'chore', 'MindAttic.Legion', 64505, '2026-10-16T01:52:34.187Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a11f2377b4047bb51', 'G.AWPN-T180', 'fix', 'GTFO.EZ', 136891, '2026-10-20T01:04:37.965Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a20be145d2b240cfc', 'G.AWPN-T176', 'fix', 'GTFO.EZ', 60398, '2026-10-20T00:54:33.81Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a43ff014bd35a22fa', 'G.EZ-T87', 'fix', 'GTFO.EZ', 48087, '2026-10-19T22:37:00.868Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a4cde1d04d7e374e2', 'G.EZ-T10', 'feature', 'GTFO.EZ', 36953, '2026-10-20T00:52:37.285Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a54a82f9d11c041cb', 'GAM-T42', 'fix', 'GTFO.EZ', 29422, '2026-10-20T00:53:35.432Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a57123e7adacb89ea', 'GAM-T38', 'docs', 'GTFO.EZ', 26154, '2026-10-20T00:53:01.461Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a62eb4ccb7cd966c9', 'G.AWPN-T177', 'fix', 'GTFO.EZ', 33649, '2026-10-20T00:53:33.565Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a73afae4faa18fa35', 'G.AWPN-T178', 'fix', 'GTFO.EZ', 38749, '2026-10-20T00:53:58.532Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a864b77575f480648', 'G.AWPN-T11', 'docs', 'GTFO.EZ', 188657, '2026-10-20T01:08:52.746Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-ae342d9d9099ea5fc', 'G.AWPN-T179', 'docs', 'GTFO.EZ', 85781, '2026-10-20T00:55:53.323Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-ae7cb7ce9c4062654', 'G.EZ-T89', 'fix', 'GTFO.EZ', 28808, '2026-10-19T22:37:16.677Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-af34c935f3c86ea3c', 'G.EZ-T88', 'fix', 'GTFO.EZ', 26876, '2026-10-19T22:37:12.412Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b18c3d91-d1ee-498f-9b2a-ab33e3b645f2', 'GE-T10', 'feature', 'GTFO.EZ', 167981, '2026-10-15T10:07:24.869Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b2058fc7-0630-4bfb-8a82-e52eb10dc954', 'SS-T58', 'chore', 'Prose', 410975, '2026-10-15T19:31:24.035Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b4b53ca8-20ef-43fa-8a06-0410b550846b', 'USS-T38', 'feature', 'UnitySolarSystem', 332571, '2026-10-15T11:15:54.182Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b6555e92-208c-40e1-97f9-9c2a416e26cb', 'GG-T21', 'chore', 'GridGameGaiden', 236676, '2026-10-16T02:25:11.71Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b69654b3-c0f4-4aee-be68-c271092bdc77', 'AUT-T8', 'chore', 'Automata', 157577, '2026-10-16T00:37:04.422Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b6fdf093-f759-4e26-9ada-b18103940feb', 'SS-T39', 'chore', 'Prose', 248565, '2026-10-15T13:00:53.667Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('b71d1b8e-1724-4424-9fbd-b4f425f4eaf9', 'GG-T54', 'feature', 'GridGameGaiden', 213962, '2026-10-15T16:10:21.615Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('bbaff692-95da-4fb8-a66a-886e74765235', 'GG-T29', 'chore', 'GridGameGaiden', 74311, '2026-10-16T01:35:40.176Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('bc5df24e-48f0-44b2-a625-73ccd22895f3', 'SS-T64', 'chore', 'Prose', 57700, '2026-10-15T16:42:12.843Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('bd617946-ff52-4e58-97ae-2a6cf5699b0b', 'SS-T93', 'chore', 'Prose', 460014, '2026-10-15T20:39:39.816Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('bd8c4d39-d4df-435b-867b-23ae59638fa7', 'FOR-T2', 'chore', 'Formicarium', 93531, '2026-10-16T00:57:34.742Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('bf1b6349-ffc0-42de-add6-324ba0d0392d', 'U.SPC-T23', 'fix', 'UnitySolarSystem', 180149, '2026-10-15T10:11:57.346Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c03cdd6e-5737-4767-891e-bb40608ed9b8', 'M.LOGS-T2', 'chore', 'MindAttic.Log', 75415, '2026-10-16T01:07:08.683Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c37f7fea-1468-4b2b-93dc-a71f1b751e81', 'USS-T43', 'feature', 'UnitySolarSystem', 242441, '2026-10-15T11:19:51.319Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c43716d5-08d2-46b9-87db-43a2daf8bb7c', 'GG-T49', 'feature', 'GridGameGaiden', 174079, '2026-10-15T15:41:29.028Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c52da005-91f4-4da7-b153-4e97df01bea7', 'SS-T44', 'chore', 'Prose', 162599, '2026-10-15T12:43:08.817Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c58803a5-abdb-4e40-9883-67b5dba97611', 'GAD-T3', 'chore', 'Gadfly', 47328, '2026-10-16T01:38:53.102Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c5e62040-e0d6-4d45-a156-0d02c2b94109', 'SS-T59', 'chore', 'Prose', 683558, '2026-10-15T20:37:35.05Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c5f41188-8db6-46f6-9f04-990af283f260', 'SS-T92', 'chore', 'Prose', 486373, '2026-10-15T20:48:37.006Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c5fe3a24-4fc9-48ca-8a0a-209cee258da6', 'AUT-T9', 'chore', 'Automata', 126187, '2026-10-16T00:57:22.561Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c68febc3-2b04-46fb-a62a-66baae06101c', 'U.SPC-T30', 'chore', 'UnitySolarSystem', 75939, '2026-10-15T09:56:54.439Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c7681bf4-9b26-4e18-bdcc-3ce3ca3b388d', 'GG-T17', 'chore', 'GridGameGaiden', 213018, '2026-10-16T02:16:19.076Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('c79db824-3a0a-4e46-a46c-d548cbfb7f5d', 'FOR-T3', 'chore', 'Formicarium', 50950, '2026-10-16T01:28:41.25Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('cde93896-ba2d-4622-a65c-3ec191294700', 'GG-T24', 'chore', 'GridGameGaiden', 42571, '2026-10-16T01:20:19.355Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('cf74dc58-d5e2-4182-be0c-b4ab3a49c988', 'GG-T32', 'chore', 'GridGameGaiden', 129183, '2026-10-15T13:17:05.986Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d2f65320-6698-4816-a87d-6d7dba20cce7', 'SS-T3', 'chore', 'Prose', 75422, '2026-10-15T11:58:04.783Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-a32b62aba1c9e767a', 'G.EZ-T86', 'chore', 'GTFO.EZ', 49666, '2026-10-20T01:49:51.924Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-aa811cb908406606b', 'G.AWPN-T181', 'feature', 'GTFO.EZ', 79393, '2026-10-20T01:56:29.67Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-ab0597928274e16e7', 'G.EZ-T90', 'chore', 'GTFO.EZ', 110270, '2026-10-20T02:03:44.999Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-ac3ad88d78b3f206d', 'G.AWPN-T183', 'feature', 'GTFO.EZ', 60197, '2026-10-20T01:57:10.404Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d442eb2a-5100-4bee-81bb-b96218fd8e43', 'OC-T2', 'chore', 'OpenCredentials', 133990, '2026-10-16T01:22:22.22Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d528c4b0-ef4c-4c88-891b-eb12780284b6', 'AWN-T2', 'chore', 'AutoWebNav', 68820, '2026-10-16T00:55:29.283Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d5d51d7a-38fc-48c9-8d71-f82a76643098', 'GGC-T2', 'chore', 'GridGameCaravan', 61874, '2026-10-16T01:00:38.379Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a017a5f478ed06a95', 'U.SPC-T304', 'fix', 'UnitySpace', 433076, '2026-10-20T01:39:32.635Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a1ae7c0cab83181f3', 'U.SPC-T308', 'fix', 'UnitySpace', 112381, '2026-10-20T01:17:47.548Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a4f53685d71f25de6', 'U.SPC-T302', 'fix', 'UnitySpace', 283252, '2026-10-20T01:42:15.568Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a5075c678650025e6', 'U.SPC-T305', 'fix', 'UnitySpace', 124950, '2026-10-20T01:11:09.29Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a878652d2b215db74', 'U.SPC-T301', 'fix', 'UnitySpace', 347053, '2026-10-20T01:39:32.116Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a971f6020787a8ec1', 'U.SPC-T315', 'fix', 'UnitySpace', 146738, '2026-10-20T01:41:19.672Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-ae6e41aabbb78f407', 'U.SPC-T306', 'fix', 'UnitySpace', 637521, '2026-10-20T01:42:35.549Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-aec3aab9fefec46c5', 'U.SPC-T307', 'fix', 'UnitySpace', 281396, '2026-10-20T01:30:19.521Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-af06a0980b838f74e', 'U.SPC-T303', 'fix', 'UnitySpace', 396375, '2026-10-20T01:35:57.683Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('dc4addfb-4d3b-45a7-9ac2-828ff4b1782c', 'M.ORCH-T74', 'fix', 'MindAttic.Orchestrator', 52787, '2026-10-15T07:21:38.006Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('dd298eb1-9d24-4719-b110-46982a4ea713', 'GG-T27', 'chore', 'GridGameGaiden', 60700, '2026-10-16T00:39:19.341Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('dd5950c7-aab0-40f6-9e36-b0e6e19c1693', 'GG-T35', 'feature', 'GridGameGaiden', 268757, '2026-10-15T13:58:02.414Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('dd8e4d1e-8aa0-4bd0-a754-73b1b2b2fa3c', 'SS-T62', 'chore', 'Prose', 191484, '2026-10-15T17:48:34.799Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('ddecf505-0c91-42f1-bcd2-81b044ce919e', 'GG-T31', 'chore', 'GridGameGaiden', 129061, '2026-10-15T13:03:13.336Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('e1b11a13-af07-4894-afd8-bb45ee1b31ab', 'WF-T30', 'chore', 'MindAttic.Workflow', 370546, '2026-10-16T00:45:01.757Z', 1);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('e312143d-5d79-4ec4-84d6-1da06a4944ed', 'U.SPC-T29', 'chore', 'UnitySolarSystem', 43459, '2026-10-15T09:54:26.893Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('e3a783cb-464f-4dad-b7fc-d60aac35c008', 'SS-T91', 'chore', 'Prose', 232583, '2026-10-15T19:48:48.447Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('e765c806-dd7c-4542-87fb-075508a802b3', 'U.SPC-T32', 'chore', 'UnitySolarSystem', 134712, '2026-10-15T11:07:31.908Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('e836982c-1c81-453c-9f34-a86bd75201ab', 'SS-T43', 'chore', 'Prose', 159035, '2026-10-15T12:47:30.612Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('e97a0688-750c-4157-9ff6-802b3460b327', 'GG-T33', 'chore', 'GridGameGaiden', 155465, '2026-10-15T13:27:22.046Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('ea19ab98-b204-4b60-882a-3ba6753d94d1', 'GG-T17', 'chore', 'GridGameGaiden', 33842, '2026-10-15T11:19:50.931Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('ec40fb80-b7fe-45b0-b61d-f93943e1f32d', 'U.SPC-T36', 'chore', 'UnitySolarSystem', 69005, '2026-10-15T10:41:33.507Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('ed70be5d-93e8-48f1-8d68-f29ae5a92946', 'CUR-T3', 'chore', 'Cursory', 54273, '2026-10-16T01:29:26.778Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('f0ff95e0-534d-49ca-a0ba-a8ba14a45b67', 'SS-T86', 'chore', 'Prose', 513473, '2026-10-15T20:48:46.831Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('f3b42562-0547-4978-a855-a515a59cdd11', 'AUT-T6', 'chore', 'Automata', 89218, '2026-10-16T00:22:14.127Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('f6684589-5e76-4409-b91e-074791c5adf3', 'M.MDIA-T2', 'chore', 'MindAttic.Media', 58402, '2026-10-16T01:05:45.495Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('facfb962-efa9-445d-aff7-67252a0f51fe', 'GE-T10', 'feature', 'GTFO.EZ', 133135, '2026-10-15T09:00:29.513Z', 0);
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share) VALUES ('fc72c7bf-0b3c-468b-a1b9-9b2056fd39ac', 'M.TELE-T2', 'chore', 'MindAttic.Telemetry', 123716, '2026-10-16T01:13:19.81Z', 0);

-- settings
INSERT INTO settings (name, value) VALUES ('AuditEveryHours', '24');
INSERT INTO settings (name, value) VALUES ('Autopilot', 'true');
INSERT INTO settings (name, value) VALUES ('AutopilotHandMinutes', '15');
INSERT INTO settings (name, value) VALUES ('AutopilotSkip', '[]');
INSERT INTO settings (name, value) VALUES ('BlockedBy', '{}');
INSERT INTO settings (name, value) VALUES ('ContextCyclePercent', '80');
INSERT INTO settings (name, value) VALUES ('DefaultProvider', '"Claude"');
INSERT INTO settings (name, value) VALUES ('ExemptCommands', '["/throttle","/unthrottle","/deploy","/run"]');
INSERT INTO settings (name, value) VALUES ('GitHubIntake', 'false');
INSERT INTO settings (name, value) VALUES ('GroomAfterDays', '30');
INSERT INTO settings (name, value) VALUES ('KeepGoing', 'true');
INSERT INTO settings (name, value) VALUES ('MaxAgentDelaySeconds', '300');
INSERT INTO settings (name, value) VALUES ('MaxConcurrentSessions', '6');
INSERT INTO settings (name, value) VALUES ('MaxSessionsPerRepo', '2');
INSERT INTO settings (name, value) VALUES ('MaxSubagents', '16');
INSERT INTO settings (name, value) VALUES ('PaceCurveK', '0.55');
INSERT INTO settings (name, value) VALUES ('PaceProfile', '"front-loaded"');
INSERT INTO settings (name, value) VALUES ('PaceTargetPercent', '98');
INSERT INTO settings (name, value) VALUES ('Pacing', 'true');
INSERT INTO settings (name, value) VALUES ('ProviderCapPercent', '90');
INSERT INTO settings (name, value) VALUES ('QuietHours', '[]');
INSERT INTO settings (name, value) VALUES ('RepoBudgetPercent', '{}');
INSERT INTO settings (name, value) VALUES ('RepoMaxSessions', '{}');
INSERT INTO settings (name, value) VALUES ('Reservations', '{}');
INSERT INTO settings (name, value) VALUES ('ResumeAfterReset', 'true');
INSERT INTO settings (name, value) VALUES ('StaleAfterHours', '12');
INSERT INTO settings (name, value) VALUES ('ThrottleAgents', 'true');
INSERT INTO settings (name, value) VALUES ('WarnPercent', '80');
INSERT INTO settings (name, value) VALUES ('WorktreeRoot', '"D:\\Projects\\MindAttic.worktrees"');

-- assignments
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (0, 'U.SPC-T49', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T49', 'Claude', '2026-10-20T21:32:01.7790828Z', 1, NULL, NULL, NULL, 38312, 'a4a33f7b-c63b-4b78-a58b-ac731eded8b0');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (1, 'U.EVE-T17', 'UnityEve', 'D:\Projects\MindAttic.worktrees\UnityEve\U.EVE-T17', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (2, 'U.EVE-T14', 'UnityEve', 'D:\Projects\MindAttic.worktrees\UnityEve\U.EVE-T14', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (3, 'U.EVE-T15', 'UnityEve', 'D:\Projects\MindAttic.worktrees\UnityEve\U.EVE-T15', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (4, 'U.EVE-T16', 'UnityEve', 'D:\Projects\MindAttic.worktrees\UnityEve\U.EVE-T16', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (5, 'UC-T16', 'UnityCursory', 'D:\Projects\MindAttic.worktrees\UnityCursory\UC-T16', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (6, 'UM-T17', 'MindAttic.Unity.Multiplayer', 'D:\Projects\MindAttic.worktrees\MindAttic.Unity.Multiplayer\UM-T17', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (7, 'U.EVE-T20', 'UnityEve', 'D:\Projects\MindAttic.worktrees\UnityEve\U.EVE-T20', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (8, 'UM-T35', 'MindAttic.Unity.Multiplayer', 'D:\Projects\MindAttic.worktrees\MindAttic.Unity.Multiplayer\UM-T35', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (9, 'U.SPC-T361', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T361', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (10, 'GG-T158', 'GridGameGaiden', 'D:\Projects\MindAttic.worktrees\GridGameGaiden\GG-T158', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (11, 'U.SPC-T315', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T315', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (12, 'U.SPC-T317', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T317', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (13, 'U.SPC-T318', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T318', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (14, 'U.SPC-T319', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T319', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (15, 'U.SPC-T323', 'UnitySpace', 'D:\Projects\MindAttic.worktrees\UnitySpace\U.SPC-T323', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session) VALUES (16, 'UM-T25', 'MindAttic.Unity.Multiplayer', 'D:\Projects\MindAttic.worktrees\MindAttic.Unity.Multiplayer\UM-T25', 'Claude', '2026-10-21T01:34:50.2279079Z', 1, NULL, NULL, NULL, 40368, '852b98be-444e-4500-b84c-d2f69e073fad');

-- state
INSERT INTO state (name, value) VALUES ('audit', '{
  "RanUtc": "2026-10-20T22:14:24.5972623Z",
  "Took": "00:00:53.5375464",
  "ReposScanned": 58,
  "FilesScanned": 4784,
  "Drift": [
    {
      "File": ".prose/commands/show.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\show.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\show.md",
      "CanonicalCount": 52,
      "Total": 53,
      "Variants": 2,
      "Similarity": 0.022222222222222223,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Toolkit",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Toolkit\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.02564102564102564,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "SyntheticExchangeTheory",
      "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.031746031746031744,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.044444444444444446,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "TownWalker",
      "Path": "D:\\Projects\\MindAttic\\TownWalker\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.044444444444444446,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "GridGameGaiden",
      "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.05405405405405406,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.058823529411764705,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/progress.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\progress.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\progress.md",
      "CanonicalCount": 52,
      "Total": 53,
      "Variants": 2,
      "Similarity": 0.06451612903225806,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/queue.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\queue.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\queue.md",
      "CanonicalCount": 52,
      "Total": 53,
      "Variants": 2,
      "Similarity": 0.07142857142857142,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "UnityEve",
      "Path": "D:\\Projects\\MindAttic\\UnityEve\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.07547169811320754,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "UnityRonin",
      "Path": "D:\\Projects\\MindAttic\\UnityRonin\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.07547169811320754,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.08695652173913043,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/commit.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\commit.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\commit.md",
      "CanonicalCount": 52,
      "Total": 53,
      "Variants": 2,
      "Similarity": 0.09523809523809523,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Export",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Export\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.10526315789473684,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.11764705882352941,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.16666666666666666,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "AutoWebNav",
      "Path": "D:\\Projects\\MindAttic\\AutoWebNav\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "KdpPublish",
      "Path": "D:\\Projects\\MindAttic\\KdpPublish\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "ChiMesh",
      "Path": "D:\\Projects\\MindAttic\\ChiMesh\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Claudia",
      "Path": "D:\\Projects\\MindAttic\\Claudia\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Cursory",
      "Path": "D:\\Projects\\MindAttic\\Cursory\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "IdiotProof",
      "Path": "D:\\Projects\\MindAttic\\IdiotProof\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MediaButler",
      "Path": "D:\\Projects\\MindAttic\\MediaButler\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Helpers",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Helpers\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Ideas",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Launcher",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Legion",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Legion\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Psst",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Vault",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "TaxRateCollector",
      "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "ThinkTank",
      "Path": "D:\\Projects\\MindAttic\\ThinkTank\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Tutor",
      "Path": "D:\\Projects\\MindAttic\\Tutor\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 38,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Web",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.21052631578947367,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Automata",
      "Path": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "ChiMesh",
      "Path": "D:\\Projects\\MindAttic\\ChiMesh\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Claudia",
      "Path": "D:\\Projects\\MindAttic\\Claudia\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Cursory",
      "Path": "D:\\Projects\\MindAttic\\Cursory\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Formicarium",
      "Path": "D:\\Projects\\MindAttic\\Formicarium\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "IdiotProof",
      "Path": "D:\\Projects\\MindAttic\\IdiotProof\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "JellyFinPoster",
      "Path": "D:\\Projects\\MindAttic\\JellyFinPoster\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "JoyceFit",
      "Path": "D:\\Projects\\MindAttic\\JoyceFit\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MediaButler",
      "Path": "D:\\Projects\\MindAttic\\MediaButler\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Authentication",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Authentication\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Bob",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Bob\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Cryptography",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Cryptography\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Deploy",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Deploy\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Helpers",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Helpers\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Ideas",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Launcher",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Legion",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Legion\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Media",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Media\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Psst",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "MindAttic.Vault",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "OpenCredentials",
      "Path": "D:\\Projects\\MindAttic\\OpenCredentials\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "TaxRateCollector",
      "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "ThinkTank",
      "Path": "D:\\Projects\\MindAttic\\ThinkTank\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Tutor",
      "Path": "D:\\Projects\\MindAttic\\Tutor\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Automata",
      "Path": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "ChiMesh",
      "Path": "D:\\Projects\\MindAttic\\ChiMesh\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Claudia",
      "Path": "D:\\Projects\\MindAttic\\Claudia\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Cursory",
      "Path": "D:\\Projects\\MindAttic\\Cursory\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Formicarium",
      "Path": "D:\\Projects\\MindAttic\\Formicarium\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "IdiotProof",
      "Path": "D:\\Projects\\MindAttic\\IdiotProof\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "JellyFinPoster",
      "Path": "D:\\Projects\\MindAttic\\JellyFinPoster\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "JobHunt",
      "Path": "D:\\Projects\\MindAttic\\JobHunt\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "JoyceFit",
      "Path": "D:\\Projects\\MindAttic\\JoyceFit\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MediaButler",
      "Path": "D:\\Projects\\MindAttic\\MediaButler\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Authentication",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Authentication\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Bob",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Bob\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Cryptography",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Cryptography\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Deploy",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Deploy\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Helpers",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Helpers\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Ideas",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Launcher",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Legion",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Legion\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Media",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Media\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Psst",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "MindAttic.Vault",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "OpenCredentials",
      "Path": "D:\\Projects\\MindAttic\\OpenCredentials\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "TaxRateCollector",
      "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "ThinkTank",
      "Path": "D:\\Projects\\MindAttic\\ThinkTank\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Tutor",
      "Path": "D:\\Projects\\MindAttic\\Tutor\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Automata",
      "Path": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "ChiMesh",
      "Path": "D:\\Projects\\MindAttic\\ChiMesh\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Claudia",
      "Path": "D:\\Projects\\MindAttic\\Claudia\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Cursory",
      "Path": "D:\\Projects\\MindAttic\\Cursory\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Formicarium",
      "Path": "D:\\Projects\\MindAttic\\Formicarium\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "IdiotProof",
      "Path": "D:\\Projects\\MindAttic\\IdiotProof\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "JellyFinPoster",
      "Path": "D:\\Projects\\MindAttic\\JellyFinPoster\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "JobHunt",
      "Path": "D:\\Projects\\MindAttic\\JobHunt\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "JoyceFit",
      "Path": "D:\\Projects\\MindAttic\\JoyceFit\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MediaButler",
      "Path": "D:\\Projects\\MindAttic\\MediaButler\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Authentication",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Authentication\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Bob",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Bob\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Cryptography",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Cryptography\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Deploy",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Deploy\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Helpers",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Helpers\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Ideas",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Launcher",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Legion",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Legion\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Media",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Media\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Psst",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "MindAttic.Vault",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "OpenCredentials",
      "Path": "D:\\Projects\\MindAttic\\OpenCredentials\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "TaxRateCollector",
      "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "ThinkTank",
      "Path": "D:\\Projects\\MindAttic\\ThinkTank\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Tutor",
      "Path": "D:\\Projects\\MindAttic\\Tutor\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 53,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "JobHunt",
      "Path": "D:\\Projects\\MindAttic\\JobHunt\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.2857142857142857,
      "IsCustom": true
    },
    {
      "File": ".claude/skills/commit/SKILL.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.claude\\skills\\commit\\SKILL.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\IdiotProof\\.claude\\skills\\commit\\SKILL.md",
      "CanonicalCount": 3,
      "Total": 4,
      "Variants": 2,
      "Similarity": 0.38461538461538464,
      "IsCustom": false
    },
    {
      "File": ".claude/skills/discard/SKILL.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.claude\\skills\\discard\\SKILL.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\IdiotProof\\.claude\\skills\\discard\\SKILL.md",
      "CanonicalCount": 3,
      "Total": 4,
      "Variants": 2,
      "Similarity": 0.47619047619047616,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Export",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Export\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "SyntheticExchangeTheory",
      "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": ".claude/skills/revert/SKILL.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.claude\\skills\\revert\\SKILL.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\IdiotProof\\.claude\\skills\\revert\\SKILL.md",
      "CanonicalCount": 3,
      "Total": 4,
      "Variants": 2,
      "Similarity": 0.5555555555555556,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "Automata",
      "Path": "D:\\Projects\\MindAttic\\Automata\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.5555555555555556,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdItems",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdMoves",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdWeapons",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 57,
      "Variants": 17,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "TownWalker",
      "Path": "D:\\Projects\\MindAttic\\TownWalker\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "UnitySpace",
      "Path": "D:\\Projects\\MindAttic\\UnitySpace\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Web",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": ".prose/commands/unstuck.md",
      "Repo": "Unstuck",
      "Path": "D:\\Projects\\MindAttic\\Unstuck\\.prose/commands\\unstuck.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\unstuck.md",
      "CanonicalCount": 52,
      "Total": 53,
      "Variants": 2,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "GridGameGaiden",
      "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "UnityEve",
      "Path": "D:\\Projects\\MindAttic\\UnityEve\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 29,
      "Total": 36,
      "Variants": 5,
      "Similarity": 0.8,
      "IsCustom": false
    }
  ],
  "Missing": [
    {
      "File": "CLAUDE.md",
      "Repo": "Automata",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "AutoWebNav",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Formicarium",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "GTFO.EZ",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "JobHunt",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "JoyceFit",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "KdpPublish",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Bob",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Cryptography",
      "HaveIt": 30,
      "Eligible": 39,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    }
  ],
  "Duplicates": [
    {
      "Files": [
        {
          "Repo": "GTFO.CrashTestDummies",
          "Path": "D:\\Projects\\MindAttic\\GTFO.CrashTestDummies\\external\\GTFO.EZ.Shared\\KitLoot.cs",
          "Lines": 121
        },
        {
          "Repo": "GTFO.EZ",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ\\external\\GTFO.EZ.Shared\\KitLoot.cs",
          "Lines": 121
        },
        {
          "Repo": "GTFO.EZ.AbsurdItems",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\external\\GTFO.EZ.Shared\\KitLoot.cs",
          "Lines": 121
        },
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\external\\GTFO.EZ.Shared\\KitLoot.cs",
          "Lines": 121
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\external\\GTFO.EZ.Shared\\KitLoot.cs",
          "Lines": 121
        },
        {
          "Repo": "GTFO.EZ.Shared",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.Shared\\KitLoot.cs",
          "Lines": 121
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 121,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 6
    },
    {
      "Files": [
        {
          "Repo": "GTFO.CrashTestDummies",
          "Path": "D:\\Projects\\MindAttic\\GTFO.CrashTestDummies\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 73
        },
        {
          "Repo": "GTFO.EZ",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 73
        },
        {
          "Repo": "GTFO.EZ.AbsurdItems",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 73
        },
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 73
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 73
        },
        {
          "Repo": "GTFO.EZ.Shared",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 73
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 73,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 6
    },
    {
      "Files": [
        {
          "Repo": "GTFO.CrashTestDummies",
          "Path": "D:\\Projects\\MindAttic\\GTFO.CrashTestDummies\\external\\GTFO.EZ.Shared\\MessageHelper.cs",
          "Lines": 68
        },
        {
          "Repo": "GTFO.EZ",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ\\external\\GTFO.EZ.Shared\\MessageHelper.cs",
          "Lines": 68
        },
        {
          "Repo": "GTFO.EZ.AbsurdItems",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\external\\GTFO.EZ.Shared\\MessageHelper.cs",
          "Lines": 68
        },
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\external\\GTFO.EZ.Shared\\MessageHelper.cs",
          "Lines": 68
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\external\\GTFO.EZ.Shared\\MessageHelper.cs",
          "Lines": 68
        },
        {
          "Repo": "GTFO.EZ.Shared",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.Shared\\MessageHelper.cs",
          "Lines": 68
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 68,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 6
    },
    {
      "Files": [
        {
          "Repo": "Formicarium",
          "Path": "D:\\Projects\\MindAttic\\Formicarium\\dashboard\\Formicarium.Dashboard\\Components\\Layout\\ReconnectModal.razor.css",
          "Lines": 90
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\Components\\Layout\\ReconnectModal.razor.css",
          "Lines": 90
        },
        {
          "Repo": "TaxRateCollector",
          "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\TaxRateCollector.Blazor\\Components\\Layout\\ReconnectModal.razor.css",
          "Lines": 90
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 90,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "GTFO.CrashTestDummies",
          "Path": "D:\\Projects\\MindAttic\\GTFO.CrashTestDummies\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 45
        },
        {
          "Repo": "GTFO.EZ",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 45
        },
        {
          "Repo": "GTFO.EZ.AbsurdItems",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 45
        },
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 45
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 45
        },
        {
          "Repo": "GTFO.EZ.Shared",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 45
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 45,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 6
    },
    {
      "Files": [
        {
          "Repo": "ChiMesh",
          "Path": "D:\\Projects\\MindAttic\\ChiMesh\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "Cursory",
          "Path": "D:\\Projects\\MindAttic\\Cursory\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "IdiotProof",
          "Path": "D:\\Projects\\MindAttic\\IdiotProof\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MediaButler",
          "Path": "D:\\Projects\\MindAttic\\MediaButler\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Authentication",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Authentication\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Bob",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Bob\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Deploy",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Deploy\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Helpers",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Helpers\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Launcher",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Log",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Log\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Psst",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Vault",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Workflow",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Workflow\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "OpenCredentials",
          "Path": "D:\\Projects\\MindAttic\\OpenCredentials\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "TaxRateCollector",
          "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "ThinkTank",
          "Path": "D:\\Projects\\MindAttic\\ThinkTank\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\tools\\build-readme.ps1",
          "Lines": 13
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 13,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 18
    },
    {
      "Files": [
        {
          "Repo": "GridGameGaiden",
          "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "UnityEve",
          "Path": "D:\\Projects\\MindAttic\\UnityEve\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "UnitySpace",
          "Path": "D:\\Projects\\MindAttic\\UnitySpace\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 58,
      "SuggestedHome": "mindattic-agent-standard",
      "RepoCount": 4
    },
    {
      "Files": [
        {
          "Repo": "AutoWebNav",
          "Path": "D:\\Projects\\MindAttic\\AutoWebNav\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "IdiotProof",
          "Path": "D:\\Projects\\MindAttic\\IdiotProof\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "KdpPublish",
          "Path": "D:\\Projects\\MindAttic\\KdpPublish\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "OpenCredentials",
          "Path": "D:\\Projects\\MindAttic\\OpenCredentials\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 56,
      "SuggestedHome": "mindattic-agent-standard",
      "RepoCount": 4
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Log",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Log\\tools\\codex.ps1",
          "Lines": 243
        },
        {
          "Repo": "MindAttic.Vault",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\tools\\codex.ps1",
          "Lines": 243
        }
      ],
      "Identical": false,
      "Similarity": 0.7941176470588235,
      "SharedLines": 194,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Launcher",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\tools\\codex.ps1",
          "Lines": 216
        },
        {
          "Repo": "MindAttic.Workflow",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Workflow\\tools\\codex.ps1",
          "Lines": 216
        }
      ],
      "Identical": false,
      "Similarity": 0.6682464454976303,
      "SharedLines": 146,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "IdiotProof",
          "Path": "D:\\Projects\\MindAttic\\IdiotProof\\Export.ps1",
          "Lines": 135
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Export.ps1",
          "Lines": 135
        }
      ],
      "Identical": false,
      "Similarity": 0.9921875,
      "SharedLines": 132,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "GridGameGaiden",
          "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\tools\\make_icons.py",
          "Lines": 163
        },
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\tools\\make_icons.py",
          "Lines": 153
        }
      ],
      "Identical": false,
      "Similarity": 0.8445945945945946,
      "SharedLines": 130,
      "SuggestedHome": "a shared Python package",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Formicarium",
          "Path": "D:\\Projects\\MindAttic\\Formicarium\\dashboard\\Formicarium.Dashboard\\Components\\Layout\\ReconnectModal.razor.js",
          "Lines": 37
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\Components\\Layout\\ReconnectModal.razor.js",
          "Lines": 37
        },
        {
          "Repo": "TaxRateCollector",
          "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\TaxRateCollector.Blazor\\Components\\Layout\\ReconnectModal.razor.js",
          "Lines": 37
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 37,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\ryandebraal.com\\.claude\\statusline.ps1",
          "Lines": 52
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\mindatticcares.com\\.claude\\statusline.ps1",
          "Lines": 52
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\mindattic.com\\.claude\\statusline.ps1",
          "Lines": 52
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\Hyperspace\\.claude\\statusline.ps1",
          "Lines": 52
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\.prose\\statusline.ps1",
          "Lines": 52
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 52,
      "SuggestedHome": "mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserTimeout\\user-timeout.css",
          "Lines": 28
        },
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserLogin\\user-login.css",
          "Lines": 40
        },
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserCircle\\user-circle.css",
          "Lines": 43
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\wwwroot\\app.css",
          "Lines": 310
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Blazor\\wwwroot\\app.css",
          "Lines": 111
        }
      ],
      "Identical": false,
      "Similarity": 0.7894736842105263,
      "SharedLines": 103,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserTimeout\\UserTimeout.razor",
          "Lines": 28
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\Components\\UiUx\\UserTimeout.razor",
          "Lines": 28
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Shared\\Components\\UiUx\\UserTimeout.razor",
          "Lines": 28
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 28,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\deploy.ps1",
          "Lines": 128
        },
        {
          "Repo": "JobHunt",
          "Path": "D:\\Projects\\MindAttic\\JobHunt\\deploy.ps1",
          "Lines": 128
        }
      ],
      "Identical": false,
      "Similarity": 0.6260162601626016,
      "SharedLines": 82,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.App\\wwwroot\\tooltip.js",
          "Lines": 92
        },
        {
          "Repo": "IdiotProof",
          "Path": "D:\\Projects\\MindAttic\\IdiotProof\\IdiotProof.UI\\wwwroot\\js\\tooltip.js",
          "Lines": 92
        },
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\Tooltip\\tooltip.js",
          "Lines": 93
        }
      ],
      "Identical": false,
      "Similarity": 0.6436781609195402,
      "SharedLines": 80,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\wwwroot\\js\\user-timeout.js",
          "Lines": 39
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Blazor\\wwwroot\\js\\user-timeout.js",
          "Lines": 39
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 39,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\OpenAiToolCallingLlm.cs",
          "Lines": 139
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\OpenAiToolCallingLlm.cs",
          "Lines": 133
        }
      ],
      "Identical": false,
      "Similarity": 0.5625,
      "SharedLines": 77,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserCircle\\UserCircle.razor",
          "Lines": 37
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Shared\\Components\\UiUx\\UserCircle.razor",
          "Lines": 37
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 37,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserCircle\\user-circle.js",
          "Lines": 24
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\wwwroot\\js\\user-circle.js",
          "Lines": 24
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Blazor\\wwwroot\\js\\user-circle.js",
          "Lines": 24
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 24,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Tests\\Fakes\\FakeBrowserSurface.cs",
          "Lines": 35
        },
        {
          "Repo": "AutoWebNav",
          "Path": "D:\\Projects\\MindAttic\\AutoWebNav\\AutoWebNav.Tests\\Fakes\\FakeBrowserSurface.cs",
          "Lines": 35
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 35,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\AnthropicToolCallingLlm.cs",
          "Lines": 96
        },
        {
          "Repo": "MindAttic.Browser",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Browser\\src\\MindAttic.Browser\\Operator\\AnthropicToolCallingLlm.cs",
          "Lines": 80
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\AnthropicToolCallingLlm.cs",
          "Lines": 120
        }
      ],
      "Identical": false,
      "Similarity": 0.6133333333333333,
      "SharedLines": 68,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Tests\\KeyPoolFailoverTests.cs",
          "Lines": 95
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.UnitTests\\KeyPoolFailoverTests.cs",
          "Lines": 104
        }
      ],
      "Identical": false,
      "Similarity": 0.6629213483146067,
      "SharedLines": 64,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Formicarium",
          "Path": "D:\\Projects\\MindAttic\\Formicarium\\dashboard\\Formicarium.Dashboard\\Components\\Layout\\ReconnectModal.razor",
          "Lines": 30
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\Components\\Layout\\ReconnectModal.razor",
          "Lines": 30
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 30,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\Tools\\SelectFormOptionTool.cs",
          "Lines": 92
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\KdpTools\\SelectFormOptionTool.cs",
          "Lines": 105
        }
      ],
      "Identical": false,
      "Similarity": 0.6091954022988506,
      "SharedLines": 58,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Formicarium",
          "Path": "D:\\Projects\\MindAttic\\Formicarium\\dashboard\\Formicarium.Dashboard\\Components\\Pages\\Error.razor",
          "Lines": 26
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\Components\\Pages\\Error.razor",
          "Lines": 26
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 26,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\AnthropicToolClient.cs",
          "Lines": 64
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\AnthropicToolClient.cs",
          "Lines": 200
        }
      ],
      "Identical": false,
      "Similarity": 0.6779661016949152,
      "SharedLines": 45,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Tests\\UsersAdminContractTests.cs",
          "Lines": 22
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Tests\\UsersAdminContractTests.cs",
          "Lines": 22
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 22,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "AutoWebNav",
          "Path": "D:\\Projects\\MindAttic\\AutoWebNav\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "GridGameGaiden",
          "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "MindAttic.Toolkit",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Toolkit\\shared\\hooks\\quickload-on-do.ps1",
          "Lines": 57
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\ryandebraal.com\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\mindatticcares.com\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\mindattic.com\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\Hyperspace\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        },
        {
          "Repo": "ProcessLogger",
          "Path": "D:\\Projects\\MindAttic\\ProcessLogger\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 57
        },
        {
          "Repo": "SyntheticExchangeTheory",
          "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 56
        }
      ],
      "Identical": false,
      "Similarity": 0.5098039215686274,
      "SharedLines": 44,
      "SuggestedHome": "mindattic-agent-standard",
      "RepoCount": 6
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Export",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Export\\MindAttic.Export.Tests\\Migrated\\ProseInlineTests.cs",
          "Lines": 41
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.UnitTests\\ProseInlineTests.cs",
          "Lines": 85
        }
      ],
      "Identical": false,
      "Similarity": 1,
      "SharedLines": 41,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Toolkit",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Toolkit\\shared\\statusline.ps1",
          "Lines": 89
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\ryandebraal.com\\.claude\\statusline.ps1",
          "Lines": 52
        }
      ],
      "Identical": false,
      "Similarity": 0.7021276595744681,
      "SharedLines": 38,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\Tools\\ClickButtonTool.cs",
          "Lines": 55
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\KdpTools\\ClickButtonTool.cs",
          "Lines": 70
        }
      ],
      "Identical": false,
      "Similarity": 0.62,
      "SharedLines": 36,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Launcher",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\.claude\\hooks\\inject-digest.ps1",
          "Lines": 40
        },
        {
          "Repo": "MindAttic.Workflow",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Workflow\\.claude\\hooks\\inject-digest.ps1",
          "Lines": 40
        }
      ],
      "Identical": false,
      "Similarity": 0.8285714285714286,
      "SharedLines": 34,
      "SuggestedHome": "mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.App\\wwwroot\\tooltip.css",
          "Lines": 47
        },
        {
          "Repo": "IdiotProof",
          "Path": "D:\\Projects\\MindAttic\\IdiotProof\\IdiotProof.UI\\wwwroot\\css\\tooltip.css",
          "Lines": 56
        }
      ],
      "Identical": false,
      "Similarity": 0.6904761904761905,
      "SharedLines": 34,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Browser",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Browser\\src\\MindAttic.Browser\\Operator\\KeyPoolFailover.cs",
          "Lines": 16
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Core\\Services\\KeyPoolFailover.cs",
          "Lines": 16
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 16,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\UserTimeout\\user-timeout.js",
          "Lines": 40
        },
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\wwwroot\\js\\user-timeout.js",
          "Lines": 39
        }
      ],
      "Identical": false,
      "Similarity": 0.7647058823529411,
      "SharedLines": 31,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\dotnet\\src\\MindAttic.Components.Wpf\\Mvvm\\CollectionSync.cs",
          "Lines": 26
        },
        {
          "Repo": "MindAttic.Workflow",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Workflow\\src\\MindAttic.Workflow.Desktop\\ViewModels\\Mvvm.cs",
          "Lines": 43
        }
      ],
      "Identical": false,
      "Similarity": 1,
      "SharedLines": 26,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "UnityEve",
          "Path": "D:\\Projects\\MindAttic\\UnityEve\\tools\\unity.ps1",
          "Lines": 33
        },
        {
          "Repo": "UnityRonin",
          "Path": "D:\\Projects\\MindAttic\\UnityRonin\\tools\\unity.ps1",
          "Lines": 33
        }
      ],
      "Identical": false,
      "Similarity": 0.6785714285714286,
      "SharedLines": 24,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "GridGameGaiden",
          "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\Assets\\Gaiden\\App\\UI\\Tween.cs",
          "Lines": 49
        },
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\Assets\\Lab\\App\\Tween.cs",
          "Lines": 41
        }
      ],
      "Identical": false,
      "Similarity": 0.5277777777777778,
      "SharedLines": 24,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\Tools\\GetPageStatusTool.cs",
          "Lines": 40
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\KdpTools\\GetPageStatusTool.cs",
          "Lines": 57
        }
      ],
      "Identical": false,
      "Similarity": 0.5142857142857142,
      "SharedLines": 23,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "GridGameGaiden",
          "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\Assets\\Gaiden\\App\\Services\\Fonts.cs",
          "Lines": 44
        },
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\Assets\\Lab\\App\\Fonts.cs",
          "Lines": 23
        }
      ],
      "Identical": false,
      "Similarity": 0.6666666666666666,
      "SharedLines": 17,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Formicarium",
          "Path": "D:\\Projects\\MindAttic\\Formicarium\\dashboard\\Formicarium.Dashboard\\Components\\Pages\\Error.razor",
          "Lines": 26
        },
        {
          "Repo": "TaxRateCollector",
          "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\TaxRateCollector.Blazor\\Components\\Pages\\Error.razor",
          "Lines": 28
        }
      ],
      "Identical": false,
      "Similarity": 0.5714285714285714,
      "SharedLines": 17,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\IToolCallingLlm.cs",
          "Lines": 19
        },
        {
          "Repo": "MindAttic.Browser",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Browser\\src\\MindAttic.Browser\\Operator\\IToolCallingLlm.cs",
          "Lines": 21
        },
        {
          "Repo": "Prose",
          "Path": "D:\\Projects\\MindAttic\\Prose\\src\\Prose.Core\\Services\\Operator\\IToolCallingLlm.cs",
          "Lines": 34
        }
      ],
      "Identical": false,
      "Similarity": 0.6875,
      "SharedLines": 16,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 3
    },
    {
      "Files": [
        {
          "Repo": "GridGameGaiden",
          "Path": "D:\\Projects\\MindAttic\\GridGameGaiden\\tools\\unity.ps1",
          "Lines": 23
        },
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\tools\\unity.ps1",
          "Lines": 23
        }
      ],
      "Identical": false,
      "Similarity": 0.6111111111111112,
      "SharedLines": 16,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Ideas",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\src\\MindAttic.Ideas.Blazor\\Components\\Layout\\MainLayout.razor.css",
          "Lines": 15
        },
        {
          "Repo": "TaxRateCollector",
          "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\TaxRateCollector.Blazor\\Components\\Layout\\MainLayout.razor.css",
          "Lines": 89
        },
        {
          "Repo": "ThinkTank",
          "Path": "D:\\Projects\\MindAttic\\ThinkTank\\ThinkTank.Shared\\wwwroot\\app.css",
          "Lines": 1626
        },
        {
          "Repo": "Tutor",
          "Path": "D:\\Projects\\MindAttic\\Tutor\\Tutor.Shared\\wwwroot\\app.css",
          "Lines": 4971
        }
      ],
      "Identical": false,
      "Similarity": 1,
      "SharedLines": 15,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 4
    },
    {
      "Files": [
        {
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\Automata.Core\\Operator\\KeyPoolFailover.cs",
          "Lines": 16
        },
        {
          "Repo": "MindAttic.Browser",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Browser\\src\\MindAttic.Browser\\Operator\\KeyPoolFailover.cs",
          "Lines": 16
        }
      ],
      "Identical": false,
      "Similarity": 0.9090909090909091,
      "SharedLines": 15,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "IdiotProof",
          "Path": "D:\\Projects\\MindAttic\\IdiotProof\\IdiotProof.UI\\Components\\Shared\\Tooltip.razor",
          "Lines": 19
        },
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\Tooltip\\Tooltip.razor",
          "Lines": 22
        }
      ],
      "Identical": false,
      "Similarity": 0.6428571428571429,
      "SharedLines": 14,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\src\\GTFO.EZ.AbsurdMoves\\ConfigMigrations.cs",
          "Lines": 18
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\src\\GTFO.EZ.AbsurdWeapons\\ConfigMigrations.cs",
          "Lines": 43
        }
      ],
      "Identical": false,
      "Similarity": 0.6153846153846154,
      "SharedLines": 13,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    }
  ]
}');
INSERT INTO state (name, value) VALUES ('limit-wakes', '{
  "39b544f3-8a63-4109-a8ed-bba3082fa0d7@2026-10-18T20:20:00Z": "2026-10-18T20:20:00Z",
  "83178290-05ac-49b9-9e83-5fd606c9dc07@2026-10-18T20:20:00Z": "2026-10-18T20:20:00Z",
  "9ece484f-cc7a-4f8d-a535-b7b71fb92de9@2026-10-18T20:20:00Z": "2026-10-18T20:20:00Z",
  "669c23d1-3f55-4799-bdaa-ecd8351ff01e@2026-10-19T06:20:00Z": "2026-10-19T06:20:00Z",
  "1cf88640-dbf7-4b97-bb0a-a9cd74764d63@2026-10-19T19:30:00Z": "2026-10-19T19:30:00Z",
  "6daaf6b3-afd1-4b34-a6ec-a195670eef3e@2026-10-19T19:30:00Z": "2026-10-19T19:30:00Z",
  "aa65d761-0831-44de-b5c6-c7ccc143108a@2026-10-19T19:30:00Z": "2026-10-19T19:30:00Z",
  "5300d88e-be3b-441a-976c-34fc3e25e676@2026-10-20T03:50:00Z": "2026-10-20T03:50:00Z",
  "71a98525-34a0-4732-97d8-8a771755e7fe@2026-10-20T03:50:00Z": "2026-10-20T03:50:00Z",
  "71ffc829-973e-41e6-bfe0-95e4e7759eff@2026-10-20T20:30:00Z": "2026-10-20T20:30:00Z"
}');
INSERT INTO state (name, value) VALUES ('limits', '[
  {
    "Kind": 0,
    "HitUtc": "2026-09-17T05:21:20.426Z",
    "ResetUtc": "2026-09-17T07:10:00Z",
    "FreshUsed": 4387274,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-09-18T18:24:22.627Z",
    "ResetUtc": "2026-09-18T18:30:00Z",
    "FreshUsed": 2417996,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-09-18T23:15:16.914Z",
    "ResetUtc": "2026-09-19T01:20:00Z",
    "FreshUsed": 2010885,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-09-20T01:25:47.297Z",
    "ResetUtc": "2026-09-20T04:20:00Z",
    "FreshUsed": 1334554,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-09-20T11:27:21.43Z",
    "ResetUtc": "2026-09-20T12:10:00Z",
    "FreshUsed": 2293347,
    "Covered": true
  },
  {
    "Kind": 1,
    "HitUtc": "2026-09-20T22:49:14.428Z",
    "ResetUtc": "2026-09-24T21:00:00Z",
    "FreshUsed": 15397555,
    "Covered": true
  },
  {
    "Kind": 1,
    "HitUtc": "2026-09-25T10:34:00.915Z",
    "ResetUtc": "2026-09-25T14:00:00Z",
    "FreshUsed": 67228654,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-09-29T10:23:47.024Z",
    "ResetUtc": "2026-09-29T10:40:00Z",
    "FreshUsed": 2422671,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-05T05:42:25.039Z",
    "ResetUtc": "2026-10-05T08:10:00Z",
    "FreshUsed": 3835194,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-05T07:51:43.649Z",
    "ResetUtc": "2026-10-05T11:00:00Z",
    "FreshUsed": 2524137,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-05T12:58:07.606Z",
    "ResetUtc": "2026-10-05T16:00:00Z",
    "FreshUsed": 2463297,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-06T05:11:31.799Z",
    "ResetUtc": "2026-10-06T06:50:00Z",
    "FreshUsed": 2898070,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-07T18:54:16.834Z",
    "ResetUtc": "2026-10-07T19:00:00Z",
    "FreshUsed": 2977001,
    "Covered": true
  },
  {
    "Kind": 1,
    "HitUtc": "2026-10-07T22:50:24.001Z",
    "ResetUtc": "2026-10-08T21:00:00Z",
    "FreshUsed": 79030911,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-08T02:35:16.246Z",
    "ResetUtc": "2026-10-08T03:50:00Z",
    "FreshUsed": 4911255,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-08T04:57:28.488Z",
    "ResetUtc": "2026-10-08T09:30:00Z",
    "FreshUsed": 6113312,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-08T23:09:08.008Z",
    "ResetUtc": "2026-10-09T03:40:00Z",
    "FreshUsed": 2584552,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-11T10:26:39.117Z",
    "ResetUtc": "2026-10-11T11:10:00Z",
    "FreshUsed": 9249313,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-12T02:23:59.813Z",
    "ResetUtc": "2026-10-12T03:20:00Z",
    "FreshUsed": 10959964,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-13T02:30:23.617Z",
    "ResetUtc": "2026-10-13T05:10:00Z",
    "FreshUsed": 3291934,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-13T21:10:04.485Z",
    "ResetUtc": "2026-10-13T23:40:00Z",
    "FreshUsed": 11054417,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-14T03:46:12.783Z",
    "ResetUtc": "2026-10-14T04:40:00Z",
    "FreshUsed": 15444291,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-14T06:23:56.449Z",
    "ResetUtc": "2026-10-14T09:40:00Z",
    "FreshUsed": 9673640,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-14T15:04:56.306Z",
    "ResetUtc": "2026-10-14T19:00:00Z",
    "FreshUsed": 9476483,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-14T21:30:47.132Z",
    "ResetUtc": "2026-10-15T00:20:00Z",
    "FreshUsed": 11130839,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-15T11:19:56.351Z",
    "ResetUtc": "2026-10-15T11:40:00Z",
    "FreshUsed": 10205420,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-15T20:48:56.637Z",
    "ResetUtc": "2026-10-15T21:50:00Z",
    "FreshUsed": 8882093,
    "Covered": true
  },
  {
    "Kind": 0,
    "HitUtc": "2026-10-17T04:58:28.293Z",
    "ResetUtc": "2026-10-17T05:10:00Z",
    "FreshUsed": 13119483,
    "Covered": true
  }
]');
INSERT INTO state (name, value) VALUES ('pace-alarm-walls', '[
  "2026-09-17T07:10:00Z",
  "2026-09-18T18:30:00Z",
  "2026-09-19T01:20:00Z",
  "2026-09-20T04:20:00Z",
  "2026-09-20T12:10:00Z",
  "2026-09-29T10:40:00Z",
  "2026-10-05T08:10:00Z",
  "2026-10-05T11:00:00Z",
  "2026-10-05T16:00:00Z",
  "2026-10-06T06:50:00Z",
  "2026-10-07T19:00:00Z",
  "2026-10-08T03:50:00Z",
  "2026-10-08T09:30:00Z",
  "2026-10-09T03:40:00Z",
  "2026-10-11T11:10:00Z",
  "2026-10-12T03:20:00Z",
  "2026-10-13T05:10:00Z",
  "2026-10-13T23:40:00Z",
  "2026-10-14T04:40:00Z",
  "2026-10-14T09:40:00Z",
  "2026-10-14T19:00:00Z",
  "2026-10-15T00:20:00Z",
  "2026-10-15T11:40:00Z",
  "2026-10-15T21:50:00Z",
  "2026-10-17T05:10:00Z"
]');
