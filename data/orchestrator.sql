-- MindAttic.Orchestrator database dump (written with every change; the .db file is the store).

-- accounts
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', 'ohleo199@mindattic.com', 'MindAttic', 'Team · Max 5x', 2, 1792818000, 91, 1793138400, 1792800792, 1792686357.102392);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', 'ryandebraal@mindattic.com', 'MindAttic', 'Team · Max 5x', 0, 1792609200, 100, 1792764000, 1792591343, 1792591305.195613);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', 'shampoo1232@mindattic.com', 'MindAttic', 'Team · Max 5x', 51, 1792600200, 96, 1792702800, 1792589089, 1792464053.5616076);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', 'pdftransferjr@mindattic.com', 'MindAttic', 'Team · Max 5x', 81, 1792688400, 93, 1792929600, 1792685787, 1792670412.137807);

-- members
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('honey@mindattic.com', 'Honey', 'admin', 'premium', 'active', 1792771886.8054008, 1792771886.8054008);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('ohleo199@mindattic.com', 'Erin', 'admin', 'premium', 'active', 1792771886.302, 1792861312.9055407);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('pdftransferjr@mindattic.com', 'Jordan', 'owner', 'premium', 'active', 1792771887.249, 1792858876.879095);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('ryandebraal@mindattic.com', 'Ryan', 'primary-owner', 'premium', 'active', 1792771885.463, 1792840279.0027995);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('shampoo1232@mindattic.com', 'Amy', 'admin', 'premium', 'active', 1792771885.882, 1792861312.9055407);

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
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:06:35Z', 23, '2026-10-21T06:30:00Z', 3789743);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:07:09Z', 23, '2026-10-21T06:30:00Z', 3799596);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:11:15Z', 24, '2026-10-21T06:30:00Z', 3830316);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:14:26Z', 24, '2026-10-21T06:30:00Z', 4046751);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:15:30Z', 25, '2026-10-21T06:30:00Z', 4370234);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:15:35Z', 25, '2026-10-21T06:30:00Z', 4376675);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:18:35Z', 26, '2026-10-21T06:30:00Z', 4490448);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:21:35Z', 27, '2026-10-21T06:30:00Z', 4693513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:26:13Z', 28, '2026-10-21T06:30:00Z', 4756750);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:30:20Z', 29, '2026-10-21T06:30:00Z', 4851437);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:30:42Z', 29, '2026-10-21T06:30:00Z', 4923628);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:33:57Z', 30, '2026-10-21T06:30:00Z', 5273401);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:35:07Z', 32, '2026-10-21T06:30:00Z', 5921883);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:35:12Z', 32, '2026-10-21T06:30:00Z', 6113247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:35:37Z', 33, '2026-10-21T06:30:00Z', 6319486);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:37:08Z', 34, '2026-10-21T06:30:00Z', 6392520);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:39:41Z', 35, '2026-10-21T06:30:00Z', 6488832);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:42:17Z', 36, '2026-10-21T06:30:00Z', 6586496);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:44:52Z', 37, '2026-10-21T06:30:00Z', 6874874);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:44:55Z', 37, '2026-10-21T06:30:00Z', 6875600);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:45:39Z', 38, '2026-10-21T06:30:00Z', 6939143);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:46:40Z', 39, '2026-10-21T06:30:00Z', 7055637);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:47:35Z', 40, '2026-10-21T06:30:00Z', 7309407);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:49:16Z', 41, '2026-10-21T06:30:00Z', 7402676);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:51:42Z', 42, '2026-10-21T06:30:00Z', 7493219);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:54:59Z', 43, '2026-10-21T06:30:00Z', 7569348);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:56:58Z', 44, '2026-10-21T06:30:00Z', 7850436);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:57:06Z', 44, '2026-10-21T06:30:00Z', 7853002);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T02:58:19Z', 45, '2026-10-21T06:30:00Z', 8086612);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:01:38Z', 46, '2026-10-21T06:30:00Z', 8159878);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:05:49Z', 47, '2026-10-21T06:30:00Z', 8218621);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:06:58Z', 47, '2026-10-21T06:30:00Z', 8444514);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:09:51Z', 48, '2026-10-21T06:30:00Z', 8496271);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:13:55Z', 49, '2026-10-21T06:30:00Z', 8538650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:15:28Z', 49, '2026-10-21T06:30:00Z', 8657010);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:18:18Z', 50, '2026-10-21T06:30:00Z', 8751784);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:18:29Z', 50, '2026-10-21T06:30:00Z', 8795248);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:19:27Z', 51, '2026-10-21T06:30:00Z', 8972669);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:21:02Z', 52, '2026-10-21T06:30:00Z', 9136120);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:22:11Z', 53, '2026-10-21T06:30:00Z', 9218042);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:24:05Z', 54, '2026-10-21T06:30:00Z', 9321307);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:26:25Z', 55, '2026-10-21T06:30:00Z', 9381807);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:29:16Z', 56, '2026-10-21T06:30:00Z', 9469336);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:29:17Z', 56, '2026-10-21T06:30:00Z', 9469572);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:31:27Z', 57, '2026-10-21T06:30:00Z', 9546539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:33:27Z', 58, '2026-10-21T06:30:00Z', 9785161);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:36:47Z', 59, '2026-10-21T06:30:00Z', 9851518);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:40:03Z', 60, '2026-10-21T06:30:00Z', 9922367);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:42:27Z', 61, '2026-10-21T06:30:00Z', 10119237);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:44:27Z', 62, '2026-10-21T06:30:00Z', 10348700);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:44:36Z', 62, '2026-10-21T06:30:00Z', 10352279);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:48:15Z', 63, '2026-10-21T06:30:00Z', 10401960);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:52:22Z', 64, '2026-10-21T06:30:00Z', 10466554);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:56:32Z', 65, '2026-10-21T06:30:00Z', 10553180);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T03:57:06Z', 66, '2026-10-21T06:30:00Z', 11042548);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:00:27Z', 67, '2026-10-21T06:30:00Z', 11286641);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:04:41Z', 68, '2026-10-21T06:30:00Z', 11337944);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:09:00Z', 68, '2026-10-21T06:30:00Z', 11387303);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:13:12Z', 69, '2026-10-21T06:30:00Z', 11476937);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:16:21Z', 69, '2026-10-21T06:30:00Z', 11635592);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:20:24Z', 69, '2026-10-21T06:30:00Z', 11660110);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:21:07Z', 70, '2026-10-21T06:30:00Z', 11929927);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:21:18Z', 71, '2026-10-21T06:30:00Z', 11930327);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:25:26Z', 71, '2026-10-21T06:30:00Z', 11974783);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:29:31Z', 72, '2026-10-21T06:30:00Z', 12012995);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:33:36Z', 72, '2026-10-21T06:30:00Z', 12027408);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:34:10Z', 72, '2026-10-21T06:30:00Z', 12102719);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:36:50Z', 73, '2026-10-21T06:30:00Z', 12370281);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:41:08Z', 74, '2026-10-21T06:30:00Z', 12535082);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:45:16Z', 75, '2026-10-21T06:30:00Z', 12602210);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:46:48Z', 75, '2026-10-21T06:30:00Z', 12660530);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:50:18Z', 76, '2026-10-21T06:30:00Z', 12847323);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:52:43Z', 77, '2026-10-21T06:30:00Z', 13046041);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:55:28Z', 78, '2026-10-21T06:30:00Z', 13221893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T04:58:19Z', 79, '2026-10-21T06:30:00Z', 13360940);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:02:28Z', 80, '2026-10-21T06:30:00Z', 13507385);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:05:28Z', 81, '2026-10-21T06:30:00Z', 13649113);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:09:39Z', 82, '2026-10-21T06:30:00Z', 13780102);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:10:44Z', 82, '2026-10-21T06:30:00Z', 13812412);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:14:28Z', 83, '2026-10-21T06:30:00Z', 13977924);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:18:35Z', 84, '2026-10-21T06:30:00Z', 14084952);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:19:09Z', 84, '2026-10-21T06:30:00Z', 14098731);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:23:15Z', 85, '2026-10-21T06:30:00Z', 14165584);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:24:28Z', 85, '2026-10-21T06:30:00Z', 14206553);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:27:35Z', 86, '2026-10-21T06:30:00Z', 14313533);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:27:43Z', 86, '2026-10-21T06:30:00Z', 14313533);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:31:52Z', 87, '2026-10-21T06:30:00Z', 14414622);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:32:44Z', 87, '2026-10-21T06:30:00Z', 14585552);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:36:19Z', 88, '2026-10-21T06:30:00Z', 14622490);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:40:27Z', 89, '2026-10-21T06:30:00Z', 14683495);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:43:08Z', 89, '2026-10-21T06:30:00Z', 14720658);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:46:44Z', 90, '2026-10-21T06:30:00Z', 14808031);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:50:53Z', 91, '2026-10-21T06:30:00Z', 14882271);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:52:28Z', 91, '2026-10-21T06:30:00Z', 14915376);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:56:39Z', 92, '2026-10-21T06:30:00Z', 14977014);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T05:58:28Z', 92, '2026-10-21T06:30:00Z', 15007274);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:02:32Z', 93, '2026-10-21T06:30:00Z', 15072280);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:05:28Z', 93, '2026-10-21T06:30:00Z', 15111201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:09:34Z', 94, '2026-10-21T06:30:00Z', 15183768);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:11:28Z', 94, '2026-10-21T06:30:00Z', 15221962);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:15:38Z', 95, '2026-10-21T06:30:00Z', 15319046);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:17:25Z', 95, '2026-10-21T06:30:00Z', 15327951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:21:32Z', 96, '2026-10-21T06:30:00Z', 15400653);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:25:46Z', 96, '2026-10-21T06:30:00Z', 15437489);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:26:44Z', 96, '2026-10-21T06:30:00Z', 15447295);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:30:13Z', 0, '2026-10-21T11:30:00Z', 107086);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:30:35Z', 1, '2026-10-21T11:30:00Z', 208570);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:30:51Z', 1, '2026-10-21T11:30:00Z', 270483);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:31:11Z', 2, '2026-10-21T11:30:00Z', 483709);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:31:30Z', 3, '2026-10-21T11:30:00Z', 687139);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:31:42Z', 4, '2026-10-21T11:30:00Z', 862073);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:32:19Z', 5, '2026-10-21T11:30:00Z', 1156684);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:32:44Z', 6, '2026-10-21T11:30:00Z', 1352309);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:33:19Z', 7, '2026-10-21T11:30:00Z', 1458347);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:34:19Z', 8, '2026-10-21T11:30:00Z', 1671552);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:35:20Z', 9, '2026-10-21T11:30:00Z', 1768735);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:37:08Z', 10, '2026-10-21T11:30:00Z', 1855694);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:38:28Z', 11, '2026-10-21T11:30:00Z', 1950837);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:40:44Z', 12, '2026-10-21T11:30:00Z', 2028529);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:42:28Z', 13, '2026-10-21T11:30:00Z', 2156181);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:44:08Z', 14, '2026-10-21T11:30:00Z', 2271512);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:45:08Z', 15, '2026-10-21T11:30:00Z', 2361410);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:46:19Z', 16, '2026-10-21T11:30:00Z', 2476659);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:48:09Z', 17, '2026-10-21T11:30:00Z', 2569910);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:50:21Z', 18, '2026-10-21T11:30:00Z', 2700606);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:51:44Z', 19, '2026-10-21T11:30:00Z', 2757484);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:53:28Z', 20, '2026-10-21T11:30:00Z', 2827577);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:55:12Z', 21, '2026-10-21T11:30:00Z', 2885198);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:55:17Z', 21, '2026-10-21T11:30:00Z', 2888892);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:56:28Z', 22, '2026-10-21T11:30:00Z', 2946928);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T06:58:08Z', 23, '2026-10-21T11:30:00Z', 3036881);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:00:28Z', 24, '2026-10-21T11:30:00Z', 3114801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:03:20Z', 25, '2026-10-21T11:30:00Z', 3248643);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:05:44Z', 26, '2026-10-21T11:30:00Z', 3378253);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:08:28Z', 27, '2026-10-21T11:30:00Z', 3508193);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:08:33Z', 27, '2026-10-21T11:30:00Z', 3509033);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:11:29Z', 28, '2026-10-21T11:30:00Z', 3716801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:15:28Z', 29, '2026-10-21T11:30:00Z', 3912358);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:18:19Z', 30, '2026-10-21T11:30:00Z', 4089844);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:21:28Z', 31, '2026-10-21T11:30:00Z', 4382801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:22:28Z', 32, '2026-10-21T11:30:00Z', 4481653);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:24:28Z', 33, '2026-10-21T11:30:00Z', 4609189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:24:31Z', 33, '2026-10-21T11:30:00Z', 4609334);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:28:28Z', 34, '2026-10-21T11:30:00Z', 4729591);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:30:51Z', 35, '2026-10-21T11:30:00Z', 4789228);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:34:20Z', 36, '2026-10-21T11:30:00Z', 4868734);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:36:28Z', 37, '2026-10-21T11:30:00Z', 4934637);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:39:15Z', 38, '2026-10-21T11:30:00Z', 5051964);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:40:26Z', 39, '2026-10-21T11:30:00Z', 5229884);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:40:32Z', 39, '2026-10-21T11:30:00Z', 5247512);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:41:53Z', 40, '2026-10-21T11:30:00Z', 5352268);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:45:20Z', 41, '2026-10-21T11:30:00Z', 5502530);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:47:20Z', 42, '2026-10-21T11:30:00Z', 5947011);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:50:20Z', 43, '2026-10-21T11:30:00Z', 5990835);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:54:28Z', 44, '2026-10-21T11:30:00Z', 6038920);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:58:20Z', 45, '2026-10-21T11:30:00Z', 6191543);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T07:58:21Z', 45, '2026-10-21T11:30:00Z', 6195497);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:00:20Z', 46, '2026-10-21T11:30:00Z', 6394999);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:02:16Z', 47, '2026-10-21T11:30:00Z', 6476539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:06:20Z', 48, '2026-10-21T11:30:00Z', 6579360);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:08:28Z', 49, '2026-10-21T11:30:00Z', 6652376);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:11:10Z', 50, '2026-10-21T11:30:00Z', 6734840);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:15:19Z', 51, '2026-10-21T11:30:00Z', 6786797);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:15:55Z', 51, '2026-10-21T11:30:00Z', 6991369);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:19:20Z', 52, '2026-10-21T11:30:00Z', 7030866);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:23:27Z', 53, '2026-10-21T11:30:00Z', 7078154);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:26:25Z', 53, '2026-10-21T11:30:00Z', 7104435);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:29:28Z', 54, '2026-10-21T11:30:00Z', 7327490);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:31:28Z', 55, '2026-10-21T11:30:00Z', 7670713);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:35:29Z', 56, '2026-10-21T11:30:00Z', 7783749);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:39:35Z', 56, '2026-10-21T11:30:00Z', 7828645);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:43:50Z', 58, '2026-10-21T11:30:00Z', 8195120);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:47:53Z', 58, '2026-10-21T11:30:00Z', 8197302);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:52:02Z', 58, '2026-10-21T11:30:00Z', 8305751);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:56:05Z', 58, '2026-10-21T11:30:00Z', 8309912);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T08:57:45Z', 58, '2026-10-21T11:30:00Z', 8309912);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:01:50Z', 59, '2026-10-21T11:30:00Z', 8477554);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:02:44Z', 59, '2026-10-21T11:30:00Z', 8544451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:04:44Z', 60, '2026-10-21T11:30:00Z', 8830186);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:08:28Z', 61, '2026-10-21T11:30:00Z', 8995063);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:11:28Z', 62, '2026-10-21T11:30:00Z', 9067863);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:12:28Z', 63, '2026-10-21T11:30:00Z', 9272204);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:12:33Z', 63, '2026-10-21T11:30:00Z', 9273383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:13:28Z', 64, '2026-10-21T11:30:00Z', 9492978);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:14:28Z', 65, '2026-10-21T11:30:00Z', 9713286);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:15:44Z', 66, '2026-10-21T11:30:00Z', 9915042);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:16:44Z', 67, '2026-10-21T11:30:00Z', 10068767);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:19:44Z', 68, '2026-10-21T11:30:00Z', 10253458);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:23:51Z', 69, '2026-10-21T11:30:00Z', 10401210);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:24:44Z', 69, '2026-10-21T11:30:00Z', 10442610);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:27:44Z', 70, '2026-10-21T11:30:00Z', 10569225);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:30:28Z', 71, '2026-10-21T11:30:00Z', 10750449);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:34:33Z', 72, '2026-10-21T11:30:00Z', 10937071);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:34:45Z', 72, '2026-10-21T11:30:00Z', 10946449);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:38:45Z', 73, '2026-10-21T11:30:00Z', 11067991);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:40:45Z', 74, '2026-10-21T11:30:00Z', 11227083);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:44:49Z', 75, '2026-10-21T11:30:00Z', 11353639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:45:28Z', 75, '2026-10-21T11:30:00Z', 11357096);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:48:45Z', 76, '2026-10-21T11:30:00Z', 11480385);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:52:46Z', 77, '2026-10-21T11:30:00Z', 11587189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:53:45Z', 77, '2026-10-21T11:30:00Z', 11616319);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:54:28Z', 78, '2026-10-21T11:30:00Z', 12032029);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T09:58:38Z', 78, '2026-10-21T11:30:00Z', 12058092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:02:50Z', 79, '2026-10-21T11:30:00Z', 12380519);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:03:27Z', 79, '2026-10-21T11:30:00Z', 12380519);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:07:40Z', 80, '2026-10-21T11:30:00Z', 12443310);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:10:45Z', 80, '2026-10-21T11:30:00Z', 12477021);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:14:48Z', 81, '2026-10-21T11:30:00Z', 12662175);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:15:45Z', 81, '2026-10-21T11:30:00Z', 12703772);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:20:01Z', 82, '2026-10-21T11:30:00Z', 12815830);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:21:45Z', 82, '2026-10-21T11:30:00Z', 12872178);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:25:44Z', 83, '2026-10-21T11:30:00Z', 13063837);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:29:46Z', 84, '2026-10-21T11:30:00Z', 13131883);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:32:45Z', 84, '2026-10-21T11:30:00Z', 13177639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:36:45Z', 85, '2026-10-21T11:30:00Z', 13306178);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:40:45Z', 86, '2026-10-21T11:30:00Z', 13448625);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:44:53Z', 87, '2026-10-21T11:30:00Z', 13578738);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:45:45Z', 87, '2026-10-21T11:30:00Z', 13602225);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:49:45Z', 88, '2026-10-21T11:30:00Z', 13724034);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:52:45Z', 89, '2026-10-21T11:30:00Z', 13827319);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:56:50Z', 90, '2026-10-21T11:30:00Z', 13942419);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T10:57:45Z', 90, '2026-10-21T11:30:00Z', 13958142);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:01:54Z', 91, '2026-10-21T11:30:00Z', 14098030);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:02:45Z', 91, '2026-10-21T11:30:00Z', 14117068);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:06:45Z', 92, '2026-10-21T11:30:00Z', 14388343);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:07:45Z', 93, '2026-10-21T11:30:00Z', 14729664);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:07:48Z', 94, '2026-10-21T11:30:00Z', 14729664);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:11:45Z', 94, '2026-10-21T11:30:00Z', 14731647);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:15:58Z', 95, '2026-10-21T11:30:00Z', 14795061);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:19:45Z', 95, '2026-10-21T11:30:00Z', 14890476);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:23:45Z', 96, '2026-10-21T11:30:00Z', 14979649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:27:52Z', 97, '2026-10-21T11:30:00Z', 15021999);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:29:45Z', 97, '2026-10-21T11:30:00Z', 15382734);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:31:45Z', 1, '2026-10-21T16:30:00Z', 102193);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:31:54Z', 1, '2026-10-21T16:30:00Z', 102193);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:32:10.2595714Z', 2, '2026-10-21T16:30:00Z', 102193);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:33:45Z', 2, '2026-10-21T16:30:00Z', 332127);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:34:45Z', 2, '2026-10-21T16:30:00Z', 395148);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:35:45Z', 3, '2026-10-21T16:30:00Z', 457521);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:36:45Z', 3, '2026-10-21T16:30:00Z', 477328);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:37:11Z', 3, '2026-10-21T16:30:00Z', 484727);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:37:28Z', 3, '2026-10-21T16:30:00Z', 484727);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:38:12Z', 4, '2026-10-21T16:30:00Z', 599479);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:38:29Z', 4, '2026-10-21T16:30:00Z', 620954);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:39:28Z', 4, '2026-10-21T16:30:00Z', 677245);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:40:28Z', 4, '2026-10-21T16:30:00Z', 719883);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:40:45Z', 5, '2026-10-21T16:30:00Z', 737277);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:41:45Z', 5, '2026-10-21T16:30:00Z', 762767);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:42:45Z', 6, '2026-10-21T16:30:00Z', 805782);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:43:45Z', 6, '2026-10-21T16:30:00Z', 846106);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:44:45Z', 7, '2026-10-21T16:30:00Z', 881441);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:45:45Z', 7, '2026-10-21T16:30:00Z', 1164910);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:46:45Z', 8, '2026-10-21T16:30:00Z', 1208168);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:46:49.4293778Z', 9, '2026-10-21T16:30:00Z', 1210884);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:46:49Z', 9, '2026-10-21T16:30:00Z', 1210884);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:48:45Z', 9, '2026-10-21T16:30:00Z', 1265277);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:49:45Z', 9, '2026-10-21T16:30:00Z', 1293320);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:50:45Z', 9, '2026-10-21T16:30:00Z', 1328095);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:51:11.7626004Z', 10, '2026-10-21T16:30:00Z', 1344465);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:51:45Z', 10, '2026-10-21T16:30:00Z', 1347949);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:52:45Z', 10, '2026-10-21T16:30:00Z', 1362447);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:53:45Z', 10, '2026-10-21T16:30:00Z', 1401150);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:54:45Z', 11, '2026-10-21T16:30:00Z', 1428394);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:55:45Z', 11, '2026-10-21T16:30:00Z', 1459162);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:56:46Z', 12, '2026-10-21T16:30:00Z', 1498185);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:57:47Z', 12, '2026-10-21T16:30:00Z', 1504805);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:58:48Z', 12, '2026-10-21T16:30:00Z', 1543096);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T11:59:47Z', 13, '2026-10-21T16:30:00Z', 1605593);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:00:48Z', 13, '2026-10-21T16:30:00Z', 1647166);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:00:55.3555882Z', 14, '2026-10-21T16:30:00Z', 1647439);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:00:55Z', 14, '2026-10-21T16:30:00Z', 1647439);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:01:48Z', 14, '2026-10-21T16:30:00Z', 1674097);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:02:48Z', 14, '2026-10-21T16:30:00Z', 1691320);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:03:47Z', 15, '2026-10-21T16:30:00Z', 1697647);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:04:47Z', 15, '2026-10-21T16:30:00Z', 1712818);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:05:47Z', 16, '2026-10-21T16:30:00Z', 2004363);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:06:46Z', 17, '2026-10-21T16:30:00Z', 2025701);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:07:45Z', 17, '2026-10-21T16:30:00Z', 2046481);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:08:45Z', 18, '2026-10-21T16:30:00Z', 2065789);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:09:45Z', 19, '2026-10-21T16:30:00Z', 2085667);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:10:37Z', 19, '2026-10-21T16:30:00Z', 2115732);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:11:47Z', 19, '2026-10-21T16:30:00Z', 2135702);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:11:48.8567973Z', 20, '2026-10-21T16:30:00Z', 2135702);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:11:48Z', 20, '2026-10-21T16:30:00Z', 2135702);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:12:47Z', 20, '2026-10-21T16:30:00Z', 2140456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:13:47Z', 20, '2026-10-21T16:30:00Z', 2140456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:14:47Z', 20, '2026-10-21T16:30:00Z', 2140456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:15:47Z', 20, '2026-10-21T16:30:00Z', 2140456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:16:47Z', 20, '2026-10-21T16:30:00Z', 2140456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:17:12Z', 20, '2026-10-21T16:30:00Z', 2163797);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:17:23Z', 20, '2026-10-21T16:30:00Z', 2253198);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:18:16.7208213Z', 21, '2026-10-21T16:30:00Z', 2321986);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:19:30Z', 22, '2026-10-21T16:30:00Z', 3064450);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:20:30Z', 24, '2026-10-21T16:30:00Z', 3773311);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:21:30Z', 24, '2026-10-21T16:30:00Z', 3779051);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:22:30Z', 24, '2026-10-21T16:30:00Z', 3813087);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:23:30Z', 24, '2026-10-21T16:30:00Z', 3829744);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:24:30Z', 24, '2026-10-21T16:30:00Z', 3840918);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:25:30Z', 24, '2026-10-21T16:30:00Z', 3854801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:26:30Z', 24, '2026-10-21T16:30:00Z', 3865805);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:27:30Z', 25, '2026-10-21T16:30:00Z', 3882271);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:28:30Z', 25, '2026-10-21T16:30:00Z', 3893067);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:29:31Z', 25, '2026-10-21T16:30:00Z', 3906467);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:30:12Z', 25, '2026-10-21T16:30:00Z', 3927131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:30:31Z', 25, '2026-10-21T16:30:00Z', 3950998);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:30:42.1092272Z', 26, '2026-10-21T16:30:00Z', 3971786);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:30:48Z', 26, '2026-10-21T16:30:00Z', 3987437);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:31:30Z', 26, '2026-10-21T16:30:00Z', 4089429);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:32:30Z', 26, '2026-10-21T16:30:00Z', 4204858);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:32:47Z', 27, '2026-10-21T16:30:00Z', 4308514);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:33:47Z', 27, '2026-10-21T16:30:00Z', 4383218);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:34:13Z', 28, '2026-10-21T16:30:00Z', 4411199);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:34:47Z', 28, '2026-10-21T16:30:00Z', 4415500);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:35:47Z', 28, '2026-10-21T16:30:00Z', 4491999);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:36:47Z', 28, '2026-10-21T16:30:00Z', 4525599);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:37:47Z', 29, '2026-10-21T16:30:00Z', 4569228);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:38:47Z', 29, '2026-10-21T16:30:00Z', 4617222);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:39:47Z', 29, '2026-10-21T16:30:00Z', 4634431);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:40:47Z', 29, '2026-10-21T16:30:00Z', 4637393);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:41:47Z', 30, '2026-10-21T16:30:00Z', 4684492);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:42:47Z', 30, '2026-10-21T16:30:00Z', 4786837);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:43:47Z', 31, '2026-10-21T16:30:00Z', 4866400);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:44:47Z', 32, '2026-10-21T16:30:00Z', 5122647);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:44:54.1974775Z', 33, '2026-10-21T16:30:00Z', 5132772);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:44:54Z', 33, '2026-10-21T16:30:00Z', 5130675);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:45:47Z', 33, '2026-10-21T16:30:00Z', 5193416);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:46:47Z', 33, '2026-10-21T16:30:00Z', 5241809);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:47:47Z', 33, '2026-10-21T16:30:00Z', 5243979);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:48:48Z', 33, '2026-10-21T16:30:00Z', 5245237);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:49:17.8010933Z', 34, '2026-10-21T16:30:00Z', 5248193);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:49:47Z', 34, '2026-10-21T16:30:00Z', 5273529);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:50:47Z', 34, '2026-10-21T16:30:00Z', 5288299);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:51:48Z', 35, '2026-10-21T16:30:00Z', 5362217);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:52:48Z', 35, '2026-10-21T16:30:00Z', 5378648);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:53:48Z', 35, '2026-10-21T16:30:00Z', 5397006);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:54:48Z', 35, '2026-10-21T16:30:00Z', 5425940);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:55:48Z', 36, '2026-10-21T16:30:00Z', 5431131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:56:48Z', 36, '2026-10-21T16:30:00Z', 5431131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:57:11Z', 36, '2026-10-21T16:30:00Z', 5472936);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:58:22Z', 37, '2026-10-21T16:30:00Z', 5707250);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:58:30Z', 38, '2026-10-21T16:30:00Z', 5731560);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:58:34Z', 38, '2026-10-21T16:30:00Z', 5739806);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T12:59:17Z', 39, '2026-10-21T16:30:00Z', 6249170);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:00:21Z', 39, '2026-10-21T16:30:00Z', 6381170);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:00:47Z', 40, '2026-10-21T16:30:00Z', 6413270);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:01:21Z', 40, '2026-10-21T16:30:00Z', 6455056);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:02:22Z', 40, '2026-10-21T16:30:00Z', 6508451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:03:22Z', 41, '2026-10-21T16:30:00Z', 6555005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:04:21Z', 41, '2026-10-21T16:30:00Z', 6610762);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:05:21Z', 41, '2026-10-21T16:30:00Z', 6666068);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:05:47Z', 42, '2026-10-21T16:30:00Z', 6691536);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:06:21Z', 42, '2026-10-21T16:30:00Z', 6701160);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:07:21Z', 43, '2026-10-21T16:30:00Z', 6958776);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:10:48Z', 44, '2026-10-21T16:30:00Z', 7098871);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:10:55Z', 44, '2026-10-21T16:30:00Z', 7102163);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:12:48Z', 45, '2026-10-21T16:30:00Z', 7202063);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:15:22Z', 46, '2026-10-21T16:30:00Z', 7426958);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:17:22Z', 47, '2026-10-21T16:30:00Z', 7518307);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:19:49Z', 48, '2026-10-21T16:30:00Z', 7625451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:21:49Z', 49, '2026-10-21T16:30:00Z', 7831656);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:23:24Z', 50, '2026-10-21T16:30:00Z', 7927369);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:24:49Z', 51, '2026-10-21T16:30:00Z', 8142153);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:24:58Z', 51, '2026-10-21T16:30:00Z', 8144449);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:26:27Z', 52, '2026-10-21T16:30:00Z', 8243463);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:29:01Z', 53, '2026-10-21T16:30:00Z', 8348667);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:37:05Z', 0, '2026-10-21T18:30:00Z', 19148);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:41:16Z', 1, '2026-10-21T18:30:00Z', 155379);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:43:07Z', 0, '2026-10-21T18:40:00Z', 1522);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T13:58:44Z', 1, '2026-10-21T18:40:00Z', 86223);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:02:23Z', 0, '2026-10-21T19:00:00Z', 2141);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:06:24Z', 1, '2026-10-21T18:40:00Z', 100242);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:07:39Z', 1, '2026-10-21T18:40:00Z', 254211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:11:25Z', 2, '2026-10-21T18:40:00Z', 292092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:15:34Z', 2, '2026-10-21T18:40:00Z', 297408);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:23:12Z', 2, '2026-10-21T18:40:00Z', 298038);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:27:13Z', 2, '2026-10-21T18:40:00Z', 316508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:31:15Z', 3, '2026-10-21T18:40:00Z', 337886);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:34:16Z', 3, '2026-10-21T18:40:00Z', 422877);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:36:18Z', 4, '2026-10-21T18:40:00Z', 672828);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:36:30Z', 4, '2026-10-21T18:40:00Z', 741562);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:37:39Z', 5, '2026-10-21T18:40:00Z', 945234);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:38:25Z', 6, '2026-10-21T18:40:00Z', 1030568);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:39:39Z', 7, '2026-10-21T18:40:00Z', 1119259);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:40:03Z', 8, '2026-10-21T18:40:00Z', 1326991);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:41:39Z', 9, '2026-10-21T18:40:00Z', 1428543);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:43:36Z', 10, '2026-10-21T18:40:00Z', 1552764);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:43:44Z', 11, '2026-10-21T18:40:00Z', 1559879);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:44:39Z', 11, '2026-10-21T18:40:00Z', 1620288);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:46:39Z', 12, '2026-10-21T18:40:00Z', 1721438);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:48:52Z', 13, '2026-10-21T18:40:00Z', 1833346);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:52:39Z', 14, '2026-10-21T18:40:00Z', 2020394);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:52:51Z', 15, '2026-10-21T18:40:00Z', 2198398);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:53:39Z', 16, '2026-10-21T18:40:00Z', 2434332);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:53:41Z', 16, '2026-10-21T18:40:00Z', 2436990);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:54:39Z', 17, '2026-10-21T18:40:00Z', 2586036);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:55:39Z', 18, '2026-10-21T18:40:00Z', 2671714);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:57:39Z', 19, '2026-10-21T18:40:00Z', 2781571);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:58:01Z', 20, '2026-10-21T18:40:00Z', 2813187);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T14:59:39Z', 21, '2026-10-21T18:40:00Z', 2920794);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:01:39Z', 22, '2026-10-21T18:40:00Z', 3028333);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:01:42Z', 23, '2026-10-21T18:40:00Z', 3029501);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:04:39Z', 23, '2026-10-21T18:40:00Z', 3055299);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:08:42Z', 23, '2026-10-21T18:40:00Z', 3071207);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:10:39Z', 24, '2026-10-21T18:40:00Z', 3280782);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:11:33Z', 25, '2026-10-21T18:40:00Z', 3542131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:11:53Z', 26, '2026-10-21T18:40:00Z', 3771119);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:12:43Z', 27, '2026-10-21T18:40:00Z', 3984436);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:13:28Z', 28, '2026-10-21T18:40:00Z', 4111038);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:13:29Z', 28, '2026-10-21T18:40:00Z', 4111841);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:15:39Z', 29, '2026-10-21T18:40:00Z', 4223722);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:18:39Z', 30, '2026-10-21T18:40:00Z', 4334263);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:20:11Z', 31, '2026-10-21T18:40:00Z', 4393733);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:24:22Z', 32, '2026-10-21T18:40:00Z', 4497088);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:24:39Z', 32, '2026-10-21T18:40:00Z', 4500317);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:27:29Z', 33, '2026-10-21T18:40:00Z', 4559331);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:31:34Z', 34, '2026-10-21T18:40:00Z', 4612017);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:33:39Z', 34, '2026-10-21T18:40:00Z', 4652303);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:37:41Z', 35, '2026-10-21T18:40:00Z', 4718306);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:38:39Z', 35, '2026-10-21T18:40:00Z', 4734394);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:42:51Z', 36, '2026-10-21T18:40:00Z', 4804189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:43:39Z', 36, '2026-10-21T18:40:00Z', 4816649);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:47:46Z', 37, '2026-10-21T18:40:00Z', 4856295);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:48:39Z', 37, '2026-10-21T18:40:00Z', 4880547);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:52:42Z', 38, '2026-10-21T18:40:00Z', 4938098);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:53:39Z', 38, '2026-10-21T18:40:00Z', 4946627);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T15:57:39Z', 39, '2026-10-21T18:40:00Z', 5010659);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:01:17Z', 40, '2026-10-21T18:40:00Z', 5101544);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:01:18Z', 40, '2026-10-21T18:40:00Z', 5101544);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:02:39Z', 41, '2026-10-21T18:40:00Z', 5332162);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:04:39Z', 42, '2026-10-21T18:40:00Z', 5437560);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:07:39Z', 43, '2026-10-21T18:40:00Z', 5547916);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:09:39Z', 44, '2026-10-21T18:40:00Z', 5621668);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:13:39Z', 45, '2026-10-21T18:40:00Z', 5712262);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:15:39Z', 46, '2026-10-21T18:40:00Z', 5806451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:15:44Z', 46, '2026-10-21T18:40:00Z', 5806451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:17:39Z', 47, '2026-10-21T18:40:00Z', 5874950);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:20:26Z', 48, '2026-10-21T18:40:00Z', 5932818);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:21:34Z', 49, '2026-10-21T18:40:00Z', 6064991);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:22:39Z', 50, '2026-10-21T18:40:00Z', 6190051);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:24:39Z', 51, '2026-10-21T18:40:00Z', 6304312);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:26:39Z', 52, '2026-10-21T18:40:00Z', 6418100);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:26:41Z', 53, '2026-10-21T18:40:00Z', 6418545);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:27:39Z', 54, '2026-10-21T18:40:00Z', 6749592);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:29:40Z', 55, '2026-10-21T18:40:00Z', 6865754);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:30:36Z', 56, '2026-10-21T18:40:00Z', 6934220);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:31:22Z', 57, '2026-10-21T18:40:00Z', 6976071);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:32:39Z', 58, '2026-10-21T18:40:00Z', 7046648);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:32:43Z', 58, '2026-10-21T18:40:00Z', 7060767);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:34:39Z', 59, '2026-10-21T18:40:00Z', 7137994);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:36:08Z', 60, '2026-10-21T18:40:00Z', 7206781);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:37:22Z', 61, '2026-10-21T18:40:00Z', 7312630);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:39:33Z', 62, '2026-10-21T18:40:00Z', 7370405);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:41:38Z', 63, '2026-10-21T18:40:00Z', 7454299);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:45:36Z', 64, '2026-10-21T18:40:00Z', 7573015);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:45:48Z', 64, '2026-10-21T18:40:00Z', 7584512);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:47:40Z', 65, '2026-10-21T18:40:00Z', 7707275);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:51:49Z', 66, '2026-10-21T18:40:00Z', 7828016);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:54:16Z', 67, '2026-10-21T18:40:00Z', 7927634);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:56:10Z', 68, '2026-10-21T18:40:00Z', 8074034);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T16:58:11Z', 69, '2026-10-21T18:40:00Z', 8171563);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:01:02Z', 70, '2026-10-21T18:40:00Z', 8257134);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:04:34Z', 71, '2026-10-21T18:40:00Z', 8371189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:04:39Z', 71, '2026-10-21T18:40:00Z', 8371189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:09:02Z', 72, '2026-10-21T18:40:00Z', 8447047);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:13:19Z', 73, '2026-10-21T18:40:00Z', 8489369);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:16:40Z', 73, '2026-10-21T18:40:00Z', 8782968);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:18:11Z', 74, '2026-10-21T18:40:00Z', 9091057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:18:33Z', 75, '2026-10-21T18:40:00Z', 9129782);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:22:35Z', 76, '2026-10-21T18:40:00Z', 9168606);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:24:36Z', 76, '2026-10-21T18:40:00Z', 9190474);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:28:32Z', 77, '2026-10-21T18:40:00Z', 9284840);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:28:33Z', 77, '2026-10-21T18:40:00Z', 9287514);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:31:11Z', 78, '2026-10-21T18:40:00Z', 9394047);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:33:39Z', 79, '2026-10-21T18:40:00Z', 9694047);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:35:05Z', 80, '2026-10-21T18:40:00Z', 9769993);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:37:37Z', 81, '2026-10-21T18:40:00Z', 9878076);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:41:11Z', 82, '2026-10-21T18:40:00Z', 9972976);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:43:46Z', 83, '2026-10-21T18:40:00Z', 10051525);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:43:47Z', 83, '2026-10-21T18:40:00Z', 10051713);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:46:10Z', 84, '2026-10-21T18:40:00Z', 10162270);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:48:39Z', 85, '2026-10-21T18:40:00Z', 10238275);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:50:39Z', 86, '2026-10-21T18:40:00Z', 10304656);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:53:39Z', 87, '2026-10-21T18:40:00Z', 10383247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:55:39Z', 88, '2026-10-21T18:40:00Z', 10446417);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:58:39Z', 89, '2026-10-21T18:40:00Z', 10521423);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T17:58:50Z', 89, '2026-10-21T18:40:00Z', 10524304);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:00:39Z', 90, '2026-10-21T18:40:00Z', 10549490);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:03:39Z', 91, '2026-10-21T18:40:00Z', 10583501);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:07:54Z', 93, '2026-10-21T18:40:00Z', 11016428);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:10:57Z', 94, '2026-10-21T18:40:00Z', 11186830);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:15:06Z', 95, '2026-10-21T18:40:00Z', 11507846);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:19:34Z', 95, '2026-10-21T18:40:00Z', 11514146);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:20:52Z', 95, '2026-10-21T18:40:00Z', 11522546);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:24:56Z', 96, '2026-10-21T18:40:00Z', 11547733);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:29:00Z', 96, '2026-10-21T18:40:00Z', 11556260);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:30:39Z', 96, '2026-10-21T18:40:00Z', 11561646);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:34:52Z', 97, '2026-10-21T18:40:00Z', 11593092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:38:56Z', 97, '2026-10-21T18:40:00Z', 11595899);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:40:39Z', 0, '2026-10-21T23:40:00Z', 11433);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:41:10Z', 0, '2026-10-21T23:40:00Z', 17168);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:41:35Z', 1, '2026-10-21T23:40:00Z', 92567);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:42:35Z', 2, '2026-10-21T23:40:00Z', 269949);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:44:35Z', 3, '2026-10-21T23:40:00Z', 377914);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:46:24Z', 4, '2026-10-21T23:40:00Z', 466801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:49:21Z', 5, '2026-10-21T23:40:00Z', 579806);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:49:22Z', 5, '2026-10-21T23:40:00Z', 579806);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:52:53Z', 6, '2026-10-21T23:40:00Z', 677520);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:54:42Z', 7, '2026-10-21T23:40:00Z', 759754);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:57:04Z', 8, '2026-10-21T23:40:00Z', 895561);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T18:59:39Z', 9, '2026-10-21T23:40:00Z', 1002197);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:01:32Z', 10, '2026-10-21T23:40:00Z', 1109702);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:04:04Z', 11, '2026-10-21T23:40:00Z', 1208258);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:04:08Z', 11, '2026-10-21T23:40:00Z', 1211201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:06:04Z', 12, '2026-10-21T23:40:00Z', 1296545);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:08:40Z', 13, '2026-10-21T23:40:00Z', 1402706);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:09:39Z', 14, '2026-10-21T23:40:00Z', 1477456);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:10:40Z', 15, '2026-10-21T23:40:00Z', 1642596);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:12:40Z', 16, '2026-10-21T23:40:00Z', 1720581);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:12:41Z', 17, '2026-10-21T23:40:00Z', 1720581);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:14:04Z', 17, '2026-10-21T23:40:00Z', 1749621);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:16:19Z', 18, '2026-10-21T23:40:00Z', 1823010);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:19:39Z', 19, '2026-10-21T23:40:00Z', 1889498);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:20:58Z', 20, '2026-10-21T23:40:00Z', 2062962);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:22:34Z', 21, '2026-10-21T23:40:00Z', 2184180);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:24:40Z', 22, '2026-10-21T23:40:00Z', 2262659);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:24:51Z', 22, '2026-10-21T23:40:00Z', 2269994);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:27:04Z', 23, '2026-10-21T23:40:00Z', 2315344);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:31:10Z', 24, '2026-10-21T23:40:00Z', 2437065);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:33:24Z', 25, '2026-10-21T23:40:00Z', 2590876);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:34:40Z', 26, '2026-10-21T23:40:00Z', 2688548);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:37:40Z', 27, '2026-10-21T23:40:00Z', 2855156);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:39:40Z', 28, '2026-10-21T23:40:00Z', 2998151);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:43:40Z', 29, '2026-10-21T23:40:00Z', 3109511);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:43:47Z', 29, '2026-10-21T23:40:00Z', 3114014);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:45:40Z', 30, '2026-10-21T23:40:00Z', 3347720);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:48:04Z', 31, '2026-10-21T23:40:00Z', 3425481);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:50:21Z', 32, '2026-10-21T23:40:00Z', 3491291);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:53:40Z', 33, '2026-10-21T23:40:00Z', 3550347);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:55:44Z', 34, '2026-10-21T23:40:00Z', 3681795);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T19:59:57Z', 35, '2026-10-21T23:40:00Z', 3740363);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:01:24Z', 35, '2026-10-21T23:40:00Z', 3975940);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:05:40Z', 36, '2026-10-21T23:40:00Z', 4033442);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:06:40Z', 36, '2026-10-21T23:40:00Z', 4041843);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:10:40Z', 37, '2026-10-21T23:40:00Z', 4186011);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:13:12Z', 38, '2026-10-21T23:40:00Z', 4300693);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:16:25Z', 39, '2026-10-21T23:40:00Z', 4446918);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:19:04Z', 40, '2026-10-21T23:40:00Z', 4603258);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:21:40Z', 41, '2026-10-21T23:40:00Z', 4790531);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:21:43Z', 42, '2026-10-21T23:40:00Z', 4795146);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:24:06Z', 42, '2026-10-21T23:40:00Z', 4866173);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:28:01Z', 43, '2026-10-21T23:40:00Z', 4944266);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:30:06Z', 44, '2026-10-21T23:40:00Z', 5174925);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:32:06Z', 45, '2026-10-21T23:40:00Z', 5304113);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:33:06Z', 46, '2026-10-21T23:40:00Z', 5474990);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:34:04Z', 47, '2026-10-21T23:40:00Z', 5842336);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:34:10Z', 48, '2026-10-21T23:40:00Z', 5852997);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:35:04Z', 48, '2026-10-21T23:40:00Z', 5890093);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:36:04Z', 49, '2026-10-21T23:40:00Z', 5980453);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:38:07Z', 50, '2026-10-21T23:40:00Z', 6055182);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:41:38Z', 51, '2026-10-21T23:40:00Z', 6147319);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:44:15Z', 52, '2026-10-21T23:40:00Z', 6302687);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:46:07Z', 53, '2026-10-21T23:40:00Z', 6432258);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:46:21Z', 54, '2026-10-21T23:40:00Z', 6755061);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:47:04Z', 54, '2026-10-21T23:40:00Z', 6761171);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:49:06Z', 55, '2026-10-21T23:40:00Z', 6814469);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:53:15Z', 56, '2026-10-21T23:40:00Z', 6856818);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:54:06Z', 56, '2026-10-21T23:40:00Z', 6867526);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:57:02Z', 57, '2026-10-21T23:40:00Z', 6925708);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T20:58:40Z', 58, '2026-10-21T23:40:00Z', 7097754);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:00:40Z', 59, '2026-10-21T23:40:00Z', 7164171);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:00:42Z', 59, '2026-10-21T23:40:00Z', 7164513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:03:06Z', 60, '2026-10-21T23:40:00Z', 7213111);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:05:40Z', 61, '2026-10-21T23:40:00Z', 7278348);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:08:06Z', 62, '2026-10-21T23:40:00Z', 7315909);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:11:06Z', 63, '2026-10-21T23:40:00Z', 7371325);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:15:24Z', 64, '2026-10-21T23:40:00Z', 7451090);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:19:32Z', 64, '2026-10-21T23:40:00Z', 7494347);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:23:43Z', 65, '2026-10-21T23:40:00Z', 7559526);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:25:06Z', 65, '2026-10-21T23:40:00Z', 7584098);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:29:08Z', 66, '2026-10-21T23:40:00Z', 7639238);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:30:06Z', 66, '2026-10-21T23:40:00Z', 7867168);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:31:06Z', 67, '2026-10-21T23:40:00Z', 7924451);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:34:06Z', 68, '2026-10-21T23:40:00Z', 8026926);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:37:06Z', 69, '2026-10-21T23:40:00Z', 8146041);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:40:06Z', 70, '2026-10-21T23:40:00Z', 8203753);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:43:06Z', 71, '2026-10-21T23:40:00Z', 8334998);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:43:09Z', 72, '2026-10-21T23:40:00Z', 8336269);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:45:06Z', 72, '2026-10-21T23:40:00Z', 8400418);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:47:06Z', 73, '2026-10-21T23:40:00Z', 8456052);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:50:08Z', 74, '2026-10-21T23:40:00Z', 8530185);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:54:06Z', 75, '2026-10-21T23:40:00Z', 8632705);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T21:57:06Z', 76, '2026-10-21T23:40:00Z', 8675787);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:00:06Z', 77, '2026-10-21T23:40:00Z', 8765251);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:00:16Z', 77, '2026-10-21T23:40:00Z', 8766695);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:04:21Z', 78, '2026-10-21T23:40:00Z', 8784015);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:11:40Z', 78, '2026-10-21T23:40:00Z', 8785551);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:14:06Z', 78, '2026-10-21T23:40:00Z', 8894581);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:18:17Z', 79, '2026-10-21T23:40:00Z', 8966934);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:20:06Z', 79, '2026-10-21T23:40:00Z', 8997124);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:24:13Z', 80, '2026-10-21T23:40:00Z', 9058677);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:28:06Z', 80, '2026-10-21T23:40:00Z', 9123169);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:32:07Z', 81, '2026-10-21T23:40:00Z', 9168424);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:34:06Z', 81, '2026-10-21T23:40:00Z', 9201477);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:38:18Z', 82, '2026-10-21T23:40:00Z', 9238001);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:41:06Z', 82, '2026-10-21T23:40:00Z', 9311017);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:45:06Z', 83, '2026-10-21T23:40:00Z', 9371820);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:45:07Z', 83, '2026-10-21T23:40:00Z', 9371820);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:49:06Z', 84, '2026-10-21T23:40:00Z', 9433584);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:53:14Z', 85, '2026-10-21T23:40:00Z', 9495793);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:54:06Z', 85, '2026-10-21T23:40:00Z', 9509661);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T22:58:18Z', 86, '2026-10-21T23:40:00Z', 9534662);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:32:59Z', 86, '2026-10-21T23:40:00Z', 9537020);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:33:20Z', 86, '2026-10-21T23:40:00Z', 9540650);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:37:24Z', 87, '2026-10-21T23:40:00Z', 9713944);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:41:10Z', 0, '2026-10-22T04:40:00Z', 435);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:41:51.2008585Z', 0, '2026-10-22T04:40:00Z', 1158);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:42:06Z', 0, '2026-10-22T04:40:00Z', 1158);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:42:43Z', 1, '2026-10-22T04:40:00Z', 292461);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:43:21Z', 2, '2026-10-22T04:40:00Z', 358606);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:44:06Z', 2, '2026-10-22T04:40:00Z', 449818);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:44:54Z', 3, '2026-10-22T04:40:00Z', 477306);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:45:06Z', 3, '2026-10-22T04:40:00Z', 487980);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:46:06Z', 3, '2026-10-22T04:40:00Z', 508376);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:46:38Z', 3, '2026-10-22T04:40:00Z', 542005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:46:43Z', 3, '2026-10-22T04:40:00Z', 547539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:47:15Z', 4, '2026-10-22T04:40:00Z', 608342);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:47:45Z', 4, '2026-10-22T04:40:00Z', 721133);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:48:17Z', 5, '2026-10-22T04:40:00Z', 800037);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:48:55Z', 5, '2026-10-22T04:40:00Z', 998333);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:49:00Z', 6, '2026-10-22T04:40:00Z', 1285971);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:49:35Z', 7, '2026-10-22T04:40:00Z', 1430921);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:50:00Z', 8, '2026-10-22T04:40:00Z', 1588769);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:50:55Z', 9, '2026-10-22T04:40:00Z', 1719762);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:50:58Z', 9, '2026-10-22T04:40:00Z', 1720103);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:52:06Z', 10, '2026-10-22T04:40:00Z', 1829443);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:53:31Z', 11, '2026-10-22T04:40:00Z', 1913966);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:54:51Z', 12, '2026-10-22T04:40:00Z', 1995511);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:56:07Z', 13, '2026-10-22T04:40:00Z', 2111498);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:58:07Z', 14, '2026-10-22T04:40:00Z', 2207590);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:59:31Z', 15, '2026-10-22T04:40:00Z', 2271708);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-21T23:59:38Z', 15, '2026-10-22T04:40:00Z', 2279510);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:00:06Z', 16, '2026-10-22T04:40:00Z', 2398228);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:01:04Z', 17, '2026-10-22T04:40:00Z', 2509755);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:02:35Z', 18, '2026-10-22T04:40:00Z', 2620909);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:04:35Z', 19, '2026-10-22T04:40:00Z', 2693493);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:06:06Z', 20, '2026-10-22T04:40:00Z', 2923223);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:07:04Z', 21, '2026-10-22T04:40:00Z', 2967014);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:07:11Z', 21, '2026-10-22T04:40:00Z', 2972383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:10:03Z', 22, '2026-10-22T04:40:00Z', 3131767);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:11:06Z', 23, '2026-10-22T04:40:00Z', 3323053);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:13:06Z', 24, '2026-10-22T04:40:00Z', 3378613);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:13:35Z', 25, '2026-10-22T04:40:00Z', 3574613);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:15:06Z', 26, '2026-10-22T04:40:00Z', 3617829);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:17:06Z', 27, '2026-10-22T04:40:00Z', 3668728);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:17:15Z', 27, '2026-10-22T04:40:00Z', 3670626);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:19:06Z', 28, '2026-10-22T04:40:00Z', 3856281);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:20:48Z', 29, '2026-10-22T04:40:00Z', 3972641);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:22:06Z', 30, '2026-10-22T04:40:00Z', 4235331);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:23:43Z', 31, '2026-10-22T04:40:00Z', 4291980);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:26:42Z', 32, '2026-10-22T04:40:00Z', 4352921);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:28:29Z', 33, '2026-10-22T04:40:00Z', 4392672);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:28:37Z', 33, '2026-10-22T04:40:00Z', 4394532);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:32:36Z', 34, '2026-10-22T04:40:00Z', 4614797);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:33:36Z', 35, '2026-10-22T04:40:00Z', 4899720);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:34:36Z', 36, '2026-10-22T04:40:00Z', 5125977);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:36:36Z', 37, '2026-10-22T04:40:00Z', 5285125);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:39:05Z', 38, '2026-10-22T04:40:00Z', 5422427);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:41:04Z', 39, '2026-10-22T04:40:00Z', 5724505);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:41:16Z', 39, '2026-10-22T04:40:00Z', 5724505);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:44:35Z', 40, '2026-10-22T04:40:00Z', 5832557);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:48:44Z', 41, '2026-10-22T04:40:00Z', 5944434);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:49:04Z', 41, '2026-10-22T04:40:00Z', 5944434);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:53:21Z', 42, '2026-10-22T04:40:00Z', 6034997);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:54:04Z', 42, '2026-10-22T04:40:00Z', 6042107);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:58:13Z', 43, '2026-10-22T04:40:00Z', 6148305);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T00:59:04Z', 43, '2026-10-22T04:40:00Z', 6171641);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:03:10Z', 44, '2026-10-22T04:40:00Z', 6304789);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:04:04Z', 44, '2026-10-22T04:40:00Z', 6356893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:08:08Z', 45, '2026-10-22T04:40:00Z', 6485516);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:09:04Z', 45, '2026-10-22T04:40:00Z', 6514675);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:12:33Z', 46, '2026-10-22T04:40:00Z', 6712355);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:14:04Z', 47, '2026-10-22T04:40:00Z', 7015164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:16:04Z', 48, '2026-10-22T04:40:00Z', 7230373);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:19:04Z', 49, '2026-10-22T04:40:00Z', 7424886);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:23:04Z', 50, '2026-10-22T04:40:00Z', 7561859);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:26:04Z', 51, '2026-10-22T04:40:00Z', 7795014);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:26:14Z', 51, '2026-10-22T04:40:00Z', 7796409);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:28:04Z', 52, '2026-10-22T04:40:00Z', 7908366);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:32:17Z', 53, '2026-10-22T04:40:00Z', 8007337);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:33:04Z', 53, '2026-10-22T04:40:00Z', 8045678);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:35:26Z', 54, '2026-10-22T04:40:00Z', 8350538);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:39:19Z', 55, '2026-10-22T04:40:00Z', 8759869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:41:44Z', 56, '2026-10-22T04:40:00Z', 9064917);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:44:50Z', 57, '2026-10-22T04:40:00Z', 9071521);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:49:04Z', 58, '2026-10-22T04:40:00Z', 9150855);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:51:51Z', 58, '2026-10-22T04:40:00Z', 9199327);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:54:41Z', 59, '2026-10-22T04:40:00Z', 9317871);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:58:55Z', 60, '2026-10-22T04:40:00Z', 9413384);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T01:59:54Z', 60, '2026-10-22T04:40:00Z', 9511556);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:03:04Z', 61, '2026-10-22T04:40:00Z', 9666179);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:06:39Z', 62, '2026-10-22T04:40:00Z', 9736549);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:10:36Z', 63, '2026-10-22T04:40:00Z', 9943500);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:12:36Z', 64, '2026-10-22T04:40:00Z', 10178761);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:12:43Z', 64, '2026-10-22T04:40:00Z', 10182617);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:14:04Z', 65, '2026-10-22T04:40:00Z', 10341849);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:16:04Z', 66, '2026-10-22T04:40:00Z', 10463212);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:20:08Z', 67, '2026-10-22T04:40:00Z', 10534044);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:22:07Z', 67, '2026-10-22T04:40:00Z', 10554864);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:26:08Z', 68, '2026-10-22T04:40:00Z', 10625974);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:27:36Z', 68, '2026-10-22T04:40:00Z', 10662075);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:30:04Z', 69, '2026-10-22T04:40:00Z', 10802588);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:34:08Z', 70, '2026-10-22T04:40:00Z', 10874887);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:35:07Z', 70, '2026-10-22T04:40:00Z', 10895797);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:39:15Z', 71, '2026-10-22T04:40:00Z', 11011493);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:39:42Z', 71, '2026-10-22T04:40:00Z', 11066615);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:41:39Z', 72, '2026-10-22T04:40:00Z', 11209444);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:43:53Z', 73, '2026-10-22T04:40:00Z', 11304353);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:46:43Z', 74, '2026-10-22T04:40:00Z', 11390609);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:49:29Z', 75, '2026-10-22T04:40:00Z', 11554259);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:51:29Z', 76, '2026-10-22T04:40:00Z', 11645049);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:51:31Z', 76, '2026-10-22T04:40:00Z', 11645049);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:55:04Z', 77, '2026-10-22T04:40:00Z', 11709363);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T02:57:36Z', 78, '2026-10-22T04:40:00Z', 11803980);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:00:53Z', 79, '2026-10-22T04:40:00Z', 11875800);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:03:44Z', 80, '2026-10-22T04:40:00Z', 11955003);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:07:10Z', 81, '2026-10-22T04:40:00Z', 12019970);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:11:07Z', 82, '2026-10-22T04:40:00Z', 12068547);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:11:23Z', 82, '2026-10-22T04:40:00Z', 12071778);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:15:07Z', 83, '2026-10-22T04:40:00Z', 12156365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:18:50Z', 84, '2026-10-22T04:40:00Z', 12265619);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:23:15Z', 85, '2026-10-22T04:40:00Z', 12368204);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:27:21Z', 86, '2026-10-22T04:40:00Z', 12460480);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:29:03Z', 86, '2026-10-22T04:40:00Z', 12477655);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:33:07Z', 87, '2026-10-22T04:40:00Z', 12555383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:34:29Z', 87, '2026-10-22T04:40:00Z', 12602133);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:38:46Z', 88, '2026-10-22T04:40:00Z', 12683034);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:42:49Z', 89, '2026-10-22T04:40:00Z', 12721685);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:45:11Z', 89, '2026-10-22T04:40:00Z', 12773425);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:49:07Z', 90, '2026-10-22T04:40:00Z', 12845458);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:53:23Z', 91, '2026-10-22T04:40:00Z', 12922407);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:54:25Z', 91, '2026-10-22T04:40:00Z', 12936816);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T03:58:31Z', 92, '2026-10-22T04:40:00Z', 12982408);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:00:20Z', 92, '2026-10-22T04:40:00Z', 12996639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:02:05Z', 93, '2026-10-22T04:40:00Z', 13020296);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:06:22Z', 94, '2026-10-22T04:40:00Z', 13467346);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:07:15Z', 94, '2026-10-22T04:40:00Z', 13467346);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:15:42Z', 94, '2026-10-22T04:40:00Z', 13482843);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:19:53Z', 95, '2026-10-22T04:40:00Z', 13525557);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:22:29Z', 95, '2026-10-22T04:40:00Z', 13540571);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:26:34Z', 96, '2026-10-22T04:40:00Z', 13569333);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:31:02Z', 96, '2026-10-22T04:40:00Z', 13591390);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:35:14Z', 96, '2026-10-22T04:40:00Z', 13615372);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:39:16Z', 98, '2026-10-22T04:40:00Z', 14002227);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:41:17Z', 0, '2026-10-22T09:40:00Z', 1484);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:41:39Z', 2, '2026-10-22T09:40:00Z', 652869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:41:42Z', 2, '2026-10-22T09:40:00Z', 652869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:42:25Z', 2, '2026-10-22T09:40:00Z', 655180);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:42:32Z', 2, '2026-10-22T09:40:00Z', 673380);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:42:52Z', 3, '2026-10-22T09:40:00Z', 745153);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:46:45Z', 4, '2026-10-22T09:40:00Z', 928577);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:48:28Z', 5, '2026-10-22T09:40:00Z', 1141092);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:50:07Z', 6, '2026-10-22T09:40:00Z', 1338299);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:51:20Z', 7, '2026-10-22T09:40:00Z', 1488192);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:52:28Z', 8, '2026-10-22T09:40:00Z', 1660415);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:53:28Z', 9, '2026-10-22T09:40:00Z', 1843082);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:53:38Z', 9, '2026-10-22T09:40:00Z', 1875189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:54:23Z', 10, '2026-10-22T09:40:00Z', 1988907);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:55:41Z', 11, '2026-10-22T09:40:00Z', 2122776);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:57:06Z', 12, '2026-10-22T09:40:00Z', 2260796);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:58:28Z', 13, '2026-10-22T09:40:00Z', 2391907);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T04:59:45Z', 14, '2026-10-22T09:40:00Z', 2522548);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:01:06Z', 15, '2026-10-22T09:40:00Z', 2648768);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:01:19Z', 15, '2026-10-22T09:40:00Z', 2692345);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:02:28Z', 16, '2026-10-22T09:40:00Z', 2802247);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:04:07Z', 17, '2026-10-22T09:40:00Z', 2950317);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:05:28Z', 18, '2026-10-22T09:40:00Z', 3073761);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:06:44Z', 19, '2026-10-22T09:40:00Z', 3185542);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:08:23Z', 20, '2026-10-22T09:40:00Z', 3324231);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:09:28Z', 21, '2026-10-22T09:40:00Z', 3387719);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:09:43Z', 21, '2026-10-22T09:40:00Z', 3404361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:10:50Z', 22, '2026-10-22T09:40:00Z', 3506070);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:12:32Z', 23, '2026-10-22T09:40:00Z', 3616882);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:13:38Z', 24, '2026-10-22T09:40:00Z', 3815588);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:15:01Z', 25, '2026-10-22T09:40:00Z', 3894384);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:16:29Z', 26, '2026-10-22T09:40:00Z', 3977475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:18:28Z', 27, '2026-10-22T09:40:00Z', 4089656);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:19:45Z', 28, '2026-10-22T09:40:00Z', 4144352);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:21:07Z', 29, '2026-10-22T09:40:00Z', 4258817);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:22:28Z', 30, '2026-10-22T09:40:00Z', 4360971);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:24:07Z', 31, '2026-10-22T09:40:00Z', 4551408);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:25:07Z', 32, '2026-10-22T09:40:00Z', 4598812);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:26:28Z', 33, '2026-10-22T09:40:00Z', 4680811);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:26:29Z', 33, '2026-10-22T09:40:00Z', 4683203);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:28:27Z', 34, '2026-10-22T09:40:00Z', 4776837);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:30:28Z', 35, '2026-10-22T09:40:00Z', 4858877);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:33:07Z', 36, '2026-10-22T09:40:00Z', 4949662);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:34:29Z', 37, '2026-10-22T09:40:00Z', 4994784);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:36:37Z', 38, '2026-10-22T09:40:00Z', 5257337);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:38:11Z', 39, '2026-10-22T09:40:00Z', 5318404);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:38:16Z', 39, '2026-10-22T09:40:00Z', 5325776);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:41:29Z', 40, '2026-10-22T09:40:00Z', 5393492);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:44:28Z', 41, '2026-10-22T09:40:00Z', 5463760);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:46:29Z', 42, '2026-10-22T09:40:00Z', 5547122);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:48:45Z', 43, '2026-10-22T09:40:00Z', 5611312);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:50:48Z', 44, '2026-10-22T09:40:00Z', 5667564);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:53:22Z', 45, '2026-10-22T09:40:00Z', 5737340);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:53:30Z', 45, '2026-10-22T09:40:00Z', 5737340);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:56:07Z', 46, '2026-10-22T09:40:00Z', 5816949);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:58:29Z', 47, '2026-10-22T09:40:00Z', 5860763);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T05:59:07Z', 48, '2026-10-22T09:40:00Z', 6049114);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:03:19Z', 49, '2026-10-22T09:40:00Z', 6090951);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:03:45Z', 49, '2026-10-22T09:40:00Z', 6097754);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:07:00Z', 50, '2026-10-22T09:40:00Z', 6140211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:11:07Z', 51, '2026-10-22T09:40:00Z', 6222674);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:13:07Z', 52, '2026-10-22T09:40:00Z', 6530564);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:16:07Z', 53, '2026-10-22T09:40:00Z', 6551165);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:20:09Z', 54, '2026-10-22T09:40:00Z', 6801423);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:22:51Z', 55, '2026-10-22T09:40:00Z', 6807793);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:27:04Z', 56, '2026-10-22T09:40:00Z', 6825507);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:30:07Z', 56, '2026-10-22T09:40:00Z', 6842957);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:31:37Z', 57, '2026-10-22T09:40:00Z', 6901874);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:31:44Z', 57, '2026-10-22T09:40:00Z', 6909856);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:34:28Z', 58, '2026-10-22T09:40:00Z', 6952343);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:37:55Z', 59, '2026-10-22T09:40:00Z', 6998720);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:41:07Z', 60, '2026-10-22T09:40:00Z', 7033926);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:43:35Z', 61, '2026-10-22T09:40:00Z', 7117099);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:45:45Z', 62, '2026-10-22T09:40:00Z', 7234164);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:49:28Z', 63, '2026-10-22T09:40:00Z', 7406763);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:49:33Z', 63, '2026-10-22T09:40:00Z', 7406763);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:53:49Z', 64, '2026-10-22T09:40:00Z', 7527871);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:54:07Z', 64, '2026-10-22T09:40:00Z', 7537552);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:58:26Z', 65, '2026-10-22T09:40:00Z', 7646014);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T06:59:45Z', 65, '2026-10-22T09:40:00Z', 7693355);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:04:01Z', 66, '2026-10-22T09:40:00Z', 7810282);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:05:45Z', 66, '2026-10-22T09:40:00Z', 7870386);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:10:03Z', 67, '2026-10-22T09:40:00Z', 7964969);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:12:23Z', 67, '2026-10-22T09:40:00Z', 7984678);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:12:45Z', 68, '2026-10-22T09:40:00Z', 8015672);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:16:57Z', 69, '2026-10-22T09:40:00Z', 8437158);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:20:28Z', 69, '2026-10-22T09:40:00Z', 8483741);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:24:30Z', 70, '2026-10-22T09:40:00Z', 8564680);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:25:28Z', 70, '2026-10-22T09:40:00Z', 8620282);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:29:35Z', 71, '2026-10-22T09:40:00Z', 8718559);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:32:28Z', 71, '2026-10-22T09:40:00Z', 8766347);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:36:38Z', 72, '2026-10-22T09:40:00Z', 8879373);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:37:36Z', 72, '2026-10-22T09:40:00Z', 8922949);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:41:44Z', 73, '2026-10-22T09:40:00Z', 9028062);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:42:36Z', 73, '2026-10-22T09:40:00Z', 9028062);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:46:07Z', 74, '2026-10-22T09:40:00Z', 9157198);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:50:42Z', 75, '2026-10-22T09:40:00Z', 9273115);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:54:51Z', 76, '2026-10-22T09:40:00Z', 9353605);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:56:42Z', 76, '2026-10-22T09:40:00Z', 9430986);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T07:59:27Z', 77, '2026-10-22T09:40:00Z', 9507233);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:00:00Z', 78, '2026-10-22T09:40:00Z', 9535855);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:04:11Z', 79, '2026-10-22T09:40:00Z', 9842221);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:08:17Z', 79, '2026-10-22T09:40:00Z', 9876865);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:09:42Z', 79, '2026-10-22T09:40:00Z', 9908081);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:13:42Z', 80, '2026-10-22T09:40:00Z', 10015883);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:17:46Z', 81, '2026-10-22T09:40:00Z', 10094645);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:19:29Z', 81, '2026-10-22T09:40:00Z', 10111611);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:23:31Z', 82, '2026-10-22T09:40:00Z', 10179472);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:24:29Z', 82, '2026-10-22T09:40:00Z', 10214152);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:27:29Z', 83, '2026-10-22T09:40:00Z', 10289673);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:31:36Z', 84, '2026-10-22T09:40:00Z', 10368932);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:32:29Z', 84, '2026-10-22T09:40:00Z', 10396392);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:34:39Z', 85, '2026-10-22T09:40:00Z', 10472617);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:38:39Z', 86, '2026-10-22T09:40:00Z', 10728918);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:40:39Z', 86, '2026-10-22T09:40:00Z', 10842101);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:44:44Z', 87, '2026-10-22T09:40:00Z', 10870193);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:47:45Z', 87, '2026-10-22T09:40:00Z', 10897051);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:52:02Z', 88, '2026-10-22T09:40:00Z', 10954608);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:52:39Z', 88, '2026-10-22T09:40:00Z', 10958194);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T08:56:28Z', 89, '2026-10-22T09:40:00Z', 11021165);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:00:33Z', 89, '2026-10-22T09:40:00Z', 11044168);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:02:29Z', 90, '2026-10-22T09:40:00Z', 11183361);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:06:44Z', 90, '2026-10-22T09:40:00Z', 11206592);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:10:51Z', 91, '2026-10-22T09:40:00Z', 11216117);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:13:29Z', 91, '2026-10-22T09:40:00Z', 11252097);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:17:40Z', 92, '2026-10-22T09:40:00Z', 11404243);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:18:39Z', 92, '2026-10-22T09:40:00Z', 11411544);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:22:29Z', 93, '2026-10-22T09:40:00Z', 11659524);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:22:30Z', 94, '2026-10-22T09:40:00Z', 11659962);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:26:41Z', 94, '2026-10-22T09:40:00Z', 11670576);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:28:29Z', 94, '2026-10-22T09:40:00Z', 11863641);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:32:34Z', 95, '2026-10-22T09:40:00Z', 11874521);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:34:39Z', 96, '2026-10-22T09:40:00Z', 12282615);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:41:19Z', 0, '2026-10-22T14:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:41:41Z', 0, '2026-10-22T14:40:00Z', 8298);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:42:17Z', 0, '2026-10-22T14:40:00Z', 49170);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:42:42Z', 0, '2026-10-22T14:40:00Z', 91214);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:43:28Z', 0, '2026-10-22T14:40:00Z', 285803);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:43:42Z', 0, '2026-10-22T14:40:00Z', 296132);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:44:15Z', 1, '2026-10-22T14:40:00Z', 343142);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:44:32Z', 2, '2026-10-22T14:40:00Z', 355012);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:44:41Z', 2, '2026-10-22T14:40:00Z', 398628);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:45:07Z', 2, '2026-10-22T14:40:00Z', 410795);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:45:42Z', 2, '2026-10-22T14:40:00Z', 435560);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:46:07Z', 2, '2026-10-22T14:40:00Z', 453431);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:46:42Z', 2, '2026-10-22T14:40:00Z', 468855);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:47:42Z', 2, '2026-10-22T14:40:00Z', 500993);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:48:55Z', 3, '2026-10-22T14:40:00Z', 518669);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:49:06Z', 3, '2026-10-22T14:40:00Z', 522442);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:49:11Z', 3, '2026-10-22T14:40:00Z', 525860);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:49:42Z', 3, '2026-10-22T14:40:00Z', 540703);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:50:11Z', 3, '2026-10-22T14:40:00Z', 736537);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:50:42Z', 4, '2026-10-22T14:40:00Z', 754032);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:51:14Z', 4, '2026-10-22T14:40:00Z', 764614);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:51:42Z', 4, '2026-10-22T14:40:00Z', 775619);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:52:07Z', 4, '2026-10-22T14:40:00Z', 778540);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:52:42Z', 4, '2026-10-22T14:40:00Z', 788691);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:53:13Z', 6, '2026-10-22T14:40:00Z', 1387126);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:53:42Z', 6, '2026-10-22T14:40:00Z', 1391257);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:54:42Z', 6, '2026-10-22T14:40:00Z', 1402669);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:55:14.8398283Z', 7, '2026-10-22T14:40:00Z', 1410872);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:55:14Z', 7, '2026-10-22T14:40:00Z', 1410872);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:58:07Z', 7, '2026-10-22T14:40:00Z', 1434139);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T09:59:26.8355129Z', 7, '2026-10-22T14:40:00Z', 1455338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:02:29Z', 8, '2026-10-22T14:40:00Z', 1566558);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:03:50Z', 9, '2026-10-22T14:40:00Z', 1743342);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:04:17Z', 10, '2026-10-22T14:40:00Z', 1974138);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:04:42Z', 11, '2026-10-22T14:40:00Z', 2203338);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:05:08Z', 12, '2026-10-22T14:40:00Z', 2356601);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:05:42Z', 13, '2026-10-22T14:40:00Z', 2511198);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:06:30Z', 14, '2026-10-22T14:40:00Z', 2710002);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:06:42Z', 14, '2026-10-22T14:40:00Z', 2728670);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:07:08Z', 15, '2026-10-22T14:40:00Z', 2778462);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:07:42Z', 15, '2026-10-22T14:40:00Z', 2852403);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:08:42Z', 17, '2026-10-22T14:40:00Z', 2956236);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:08:58.6113281Z', 17, '2026-10-22T14:40:00Z', 3012651);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:09:30Z', 17, '2026-10-22T14:40:00Z', 3036385);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:09:40Z', 17, '2026-10-22T14:40:00Z', 3044896);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:10:07Z', 18, '2026-10-22T14:40:00Z', 3083005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:10:42Z', 18, '2026-10-22T14:40:00Z', 3109694);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:11:13Z', 19, '2026-10-22T14:40:00Z', 3150392);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:11:42Z', 19, '2026-10-22T14:40:00Z', 3190013);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:12:45Z', 20, '2026-10-22T14:40:00Z', 3232115);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:13:30Z', 20, '2026-10-22T14:40:00Z', 3256023);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:13:42Z', 20, '2026-10-22T14:40:00Z', 3262797);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:14:30Z', 20, '2026-10-22T14:40:00Z', 3281511);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:14:46Z', 21, '2026-10-22T14:40:00Z', 3306435);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:15:30Z', 21, '2026-10-22T14:40:00Z', 3336396);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:15:42Z', 21, '2026-10-22T14:40:00Z', 3344837);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:16:05Z', 21, '2026-10-22T14:40:00Z', 3357584);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:16:17Z', 22, '2026-10-22T14:40:00Z', 3373140);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:16:27.6811938Z', 23, '2026-10-22T14:40:00Z', 3505280);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:17:21Z', 23, '2026-10-22T14:40:00Z', 3621263);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:17:42Z', 23, '2026-10-22T14:40:00Z', 3648255);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:18:42Z', 24, '2026-10-22T14:40:00Z', 3771700);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:20:08Z', 25, '2026-10-22T14:40:00Z', 3921746);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:20:42Z', 26, '2026-10-22T14:40:00Z', 3980234);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:21:08Z', 26, '2026-10-22T14:40:00Z', 3995385);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:21:42Z', 26, '2026-10-22T14:40:00Z', 4011706);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:22:45Z', 27, '2026-10-22T14:40:00Z', 4055232);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:23:42Z', 27, '2026-10-22T14:40:00Z', 4077999);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:24:05Z', 27, '2026-10-22T14:40:00Z', 4081989);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:26:08Z', 28, '2026-10-22T14:40:00Z', 4124277);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:26:10.2158713Z', 29, '2026-10-22T14:40:00Z', 4243584);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:26:40Z', 29, '2026-10-22T14:40:00Z', 4251852);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:27:42Z', 30, '2026-10-22T14:40:00Z', 4503852);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:28:30Z', 30, '2026-10-22T14:40:00Z', 4525867);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:28:42Z', 30, '2026-10-22T14:40:00Z', 4530249);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:29:42Z', 30, '2026-10-22T14:40:00Z', 4565158);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:30:42Z', 31, '2026-10-22T14:40:00Z', 4737139);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:31:23Z', 32, '2026-10-22T14:40:00Z', 5040192);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:31:42Z', 32, '2026-10-22T14:40:00Z', 5042628);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:32:30Z', 32, '2026-10-22T14:40:00Z', 5056722);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:32:42Z', 33, '2026-10-22T14:40:00Z', 5057101);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:33:19Z', 33, '2026-10-22T14:40:00Z', 5067828);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:33:42Z', 33, '2026-10-22T14:40:00Z', 5077354);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:34:11Z', 33, '2026-10-22T14:40:00Z', 5090089);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:34:42Z', 33, '2026-10-22T14:40:00Z', 5095946);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:35:07Z', 33, '2026-10-22T14:40:00Z', 5108497);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:35:22Z', 33, '2026-10-22T14:40:00Z', 5111248);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:35:42Z', 33, '2026-10-22T14:40:00Z', 5119720);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:36:30Z', 34, '2026-10-22T14:40:00Z', 5127221);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:36:33.3713398Z', 34, '2026-10-22T14:40:00Z', 5127221);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:36:42Z', 34, '2026-10-22T14:40:00Z', 5130291);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:37:42Z', 34, '2026-10-22T14:40:00Z', 5137139);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:38:30Z', 34, '2026-10-22T14:40:00Z', 5145869);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:38:42Z', 34, '2026-10-22T14:40:00Z', 5146335);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:39:13Z', 34, '2026-10-22T14:40:00Z', 5162684);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:39:42Z', 34, '2026-10-22T14:40:00Z', 5164255);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:40:42Z', 35, '2026-10-22T14:40:00Z', 5177801);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:41:42Z', 35, '2026-10-22T14:40:00Z', 5477036);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:42:42Z', 36, '2026-10-22T14:40:00Z', 5493616);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:43:42Z', 36, '2026-10-22T14:40:00Z', 5507028);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:44:42Z', 36, '2026-10-22T14:40:00Z', 5522257);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:45:45Z', 37, '2026-10-22T14:40:00Z', 5536160);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:46:07Z', 37, '2026-10-22T14:40:00Z', 5538201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:46:45Z', 37, '2026-10-22T14:40:00Z', 5547895);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:47:45Z', 37, '2026-10-22T14:40:00Z', 5575590);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:48:45Z', 37, '2026-10-22T14:40:00Z', 5607383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:49:08Z', 38, '2026-10-22T14:40:00Z', 5617720);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:49:45Z', 38, '2026-10-22T14:40:00Z', 5624645);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:50:14Z', 38, '2026-10-22T14:40:00Z', 5629651);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:50:45Z', 38, '2026-10-22T14:40:00Z', 5630568);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:51:41Z', 38, '2026-10-22T14:40:00Z', 5632663);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:52:07Z', 38, '2026-10-22T14:40:00Z', 5753449);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:52:39Z', 39, '2026-10-22T14:40:00Z', 5813568);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:52:42Z', 39, '2026-10-22T14:40:00Z', 5846017);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:53:42Z', 39, '2026-10-22T14:40:00Z', 5891977);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:54:42Z', 40, '2026-10-22T14:40:00Z', 5916573);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:54:45.6046836Z', 40, '2026-10-22T14:40:00Z', 5919237);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:54:45Z', 40, '2026-10-22T14:40:00Z', 5919237);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:55:42Z', 40, '2026-10-22T14:40:00Z', 5956138);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:56:42Z', 41, '2026-10-22T14:40:00Z', 6280167);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:57:42Z', 41, '2026-10-22T14:40:00Z', 6303550);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:58:42Z', 42, '2026-10-22T14:40:00Z', 6323057);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T10:59:42Z', 42, '2026-10-22T14:40:00Z', 6340003);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:00:42Z', 42, '2026-10-22T14:40:00Z', 6340545);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:01:42Z', 42, '2026-10-22T14:40:00Z', 6351415);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:02:42Z', 42, '2026-10-22T14:40:00Z', 6360937);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:03:42Z', 42, '2026-10-22T14:40:00Z', 6369033);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:04:42Z', 42, '2026-10-22T14:40:00Z', 6384192);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:05:16Z', 42, '2026-10-22T14:40:00Z', 6547729);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:05:42Z', 43, '2026-10-22T14:40:00Z', 6549248);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:06:41Z', 43, '2026-10-22T14:40:00Z', 6550005);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:07:45Z', 44, '2026-10-22T14:40:00Z', 6980330);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:08:07Z', 44, '2026-10-22T14:40:00Z', 7009085);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:08:45Z', 44, '2026-10-22T14:40:00Z', 7056458);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:09:07Z', 44, '2026-10-22T14:40:00Z', 7087139);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:09:45Z', 45, '2026-10-22T14:40:00Z', 7162442);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:10:45Z', 45, '2026-10-22T14:40:00Z', 7222700);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:11:45Z', 46, '2026-10-22T14:40:00Z', 7420115);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:11:47.4643468Z', 47, '2026-10-22T14:40:00Z', 7420115);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:14:45Z', 47, '2026-10-22T14:40:00Z', 7457145);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:15:45Z', 47, '2026-10-22T14:40:00Z', 7470882);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:16:07.6079064Z', 47, '2026-10-22T14:40:00Z', 7472267);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:16:45Z', 47, '2026-10-22T14:40:00Z', 7479940);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:17:07Z', 47, '2026-10-22T14:40:00Z', 7480992);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:17:45Z', 47, '2026-10-22T14:40:00Z', 7485934);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:18:45Z', 47, '2026-10-22T14:40:00Z', 7655272);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:19:45Z', 47, '2026-10-22T14:40:00Z', 7661138);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:20:22.8896684Z', 48, '2026-10-22T14:40:00Z', 7691131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:21:45Z', 49, '2026-10-22T14:40:00Z', 7788425);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:22:14Z', 49, '2026-10-22T14:40:00Z', 7856307);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:22:45Z', 49, '2026-10-22T14:40:00Z', 7893600);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:23:45Z', 49, '2026-10-22T14:40:00Z', 7918714);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:25:07Z', 52, '2026-10-22T14:40:00Z', 8617381);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:25:18Z', 53, '2026-10-22T14:40:00Z', 8619853);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:26:52Z', 53, '2026-10-22T14:40:00Z', 8773900);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:30:55Z', 54, '2026-10-22T14:40:00Z', 8833366);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:34:18Z', 54, '2026-10-22T14:40:00Z', 8873210);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:37:08Z', 55, '2026-10-22T14:40:00Z', 8999772);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:39:25Z', 56, '2026-10-22T14:40:00Z', 9089815);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:43:28Z', 57, '2026-10-22T14:40:00Z', 9125798);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:46:52Z', 57, '2026-10-22T14:40:00Z', 9169328);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:51:04Z', 59, '2026-10-22T14:40:00Z', 9407703);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:52:34Z', 59, '2026-10-22T14:40:00Z', 9460733);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T11:55:34Z', 60, '2026-10-22T14:40:00Z', 9516842);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:00:58Z', 0, '2026-10-22T17:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:15:18Z', 0, '2026-10-22T17:00:00Z', 111006);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:16:20Z', 1, '2026-10-22T17:00:00Z', 146543);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:26:45Z', 1, '2026-10-22T17:00:00Z', 149833);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:30:05Z', 2, '2026-10-22T17:00:00Z', 333201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:34:16Z', 3, '2026-10-22T17:00:00Z', 425249);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:35:05Z', 3, '2026-10-22T17:00:00Z', 443747);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:39:11Z', 4, '2026-10-22T17:00:00Z', 495333);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:40:05Z', 4, '2026-10-22T17:00:00Z', 511976);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:44:05Z', 5, '2026-10-22T17:00:00Z', 573871);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:48:06Z', 6, '2026-10-22T17:00:00Z', 612666);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:50:05Z', 6, '2026-10-22T17:00:00Z', 645175);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:53:05Z', 7, '2026-10-22T17:00:00Z', 683691);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T12:57:17Z', 7, '2026-10-22T17:00:00Z', 689462);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:15:11Z', 7, '2026-10-22T17:00:00Z', 692805);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:19:26Z', 8, '2026-10-22T17:00:00Z', 818126);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:21:12Z', 8, '2026-10-22T17:00:00Z', 855524);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:23:58Z', 9, '2026-10-22T17:00:00Z', 1127135);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:24:22Z', 10, '2026-10-22T17:00:00Z', 1266611);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:25:14Z', 11, '2026-10-22T17:00:00Z', 1474467);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:26:50Z', 12, '2026-10-22T17:00:00Z', 1681787);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:27:40Z', 13, '2026-10-22T17:00:00Z', 1786289);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:28:30Z', 14, '2026-10-22T17:00:00Z', 1919437);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:28:41Z', 15, '2026-10-22T17:00:00Z', 2002367);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:28:54Z', 15, '2026-10-22T17:00:00Z', 2145189);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:29:21Z', 16, '2026-10-22T17:00:00Z', 2266415);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:30:14Z', 18, '2026-10-22T17:00:00Z', 2466413);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:31:08Z', 19, '2026-10-22T17:00:00Z', 2605045);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:31:32Z', 20, '2026-10-22T17:00:00Z', 2673531);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:32:13Z', 21, '2026-10-22T17:00:00Z', 2777269);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:33:14Z', 22, '2026-10-22T17:00:00Z', 2917138);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:33:51Z', 23, '2026-10-22T17:00:00Z', 2998893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:34:47Z', 24, '2026-10-22T17:00:00Z', 3077696);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:35:29Z', 25, '2026-10-22T17:00:00Z', 3146992);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:36:20Z', 26, '2026-10-22T17:00:00Z', 3220097);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:36:24Z', 26, '2026-10-22T17:00:00Z', 3232201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:37:14Z', 27, '2026-10-22T17:00:00Z', 3276085);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:38:08Z', 28, '2026-10-22T17:00:00Z', 3353914);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:39:14Z', 29, '2026-10-22T17:00:00Z', 3463395);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:40:14Z', 30, '2026-10-22T17:00:00Z', 3531573);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:41:14Z', 31, '2026-10-22T17:00:00Z', 3580620);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:42:14Z', 32, '2026-10-22T17:00:00Z', 3669503);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:42:29Z', 32, '2026-10-22T17:00:00Z', 3685588);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:43:43Z', 33, '2026-10-22T17:00:00Z', 3714081);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:45:14Z', 34, '2026-10-22T17:00:00Z', 3796703);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:46:04Z', 35, '2026-10-22T17:00:00Z', 3842263);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:48:14Z', 36, '2026-10-22T17:00:00Z', 3940420);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:48:28Z', 37, '2026-10-22T17:00:00Z', 4196366);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:50:14Z', 39, '2026-10-22T17:00:00Z', 4294929);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:50:27Z', 39, '2026-10-22T17:00:00Z', 4298249);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:52:14Z', 40, '2026-10-22T17:00:00Z', 4385539);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:52:46Z', 41, '2026-10-22T17:00:00Z', 4414306);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:55:30Z', 42, '2026-10-22T17:00:00Z', 4484140);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:58:30Z', 43, '2026-10-22T17:00:00Z', 4530380);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T13:59:30Z', 44, '2026-10-22T17:00:00Z', 4626256);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:01:30Z', 45, '2026-10-22T17:00:00Z', 5016524);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:01:37Z', 46, '2026-10-22T17:00:00Z', 5018623);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:02:30Z', 46, '2026-10-22T17:00:00Z', 5029651);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:04:30Z', 47, '2026-10-22T17:00:00Z', 5104211);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:05:30Z', 48, '2026-10-22T17:00:00Z', 5153428);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:06:12Z', 49, '2026-10-22T17:00:00Z', 5202044);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:07:30Z', 50, '2026-10-22T17:00:00Z', 5255877);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:09:30Z', 51, '2026-10-22T17:00:00Z', 5317546);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:09:41Z', 51, '2026-10-22T17:00:00Z', 5321365);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:10:55Z', 52, '2026-10-22T17:00:00Z', 5350215);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:11:30Z', 53, '2026-10-22T17:00:00Z', 5720592);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:11:58Z', 54, '2026-10-22T17:00:00Z', 5729790);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:14:30Z', 55, '2026-10-22T17:00:00Z', 5796843);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:16:24Z', 56, '2026-10-22T17:00:00Z', 5838935);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:18:25Z', 57, '2026-10-22T17:00:00Z', 5889723);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:18:26Z', 57, '2026-10-22T17:00:00Z', 5889723);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:20:30Z', 58, '2026-10-22T17:00:00Z', 5918937);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:23:30Z', 59, '2026-10-22T17:00:00Z', 5962923);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:25:30Z', 60, '2026-10-22T17:00:00Z', 5996071);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:29:40Z', 61, '2026-10-22T17:00:00Z', 6016639);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:30:30Z', 61, '2026-10-22T17:00:00Z', 6022856);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:33:41Z', 62, '2026-10-22T17:00:00Z', 6082569);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:37:43Z', 63, '2026-10-22T17:00:00Z', 6169710);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:39:30Z', 64, '2026-10-22T17:00:00Z', 6838560);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:43:30Z', 65, '2026-10-22T17:00:00Z', 6846986);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:47:32Z', 65, '2026-10-22T17:00:00Z', 6890944);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:50:30Z', 66, '2026-10-22T17:00:00Z', 6925192);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:52:30Z', 67, '2026-10-22T17:00:00Z', 6981204);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:55:30Z', 68, '2026-10-22T17:00:00Z', 7032651);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T14:59:45Z', 69, '2026-10-22T17:00:00Z', 7097787);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:01:30Z', 69, '2026-10-22T17:00:00Z', 7125554);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:05:17Z', 70, '2026-10-22T17:00:00Z', 7233621);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:09:31Z', 71, '2026-10-22T17:00:00Z', 7386268);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:13:40Z', 72, '2026-10-22T17:00:00Z', 7448063);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:16:30Z', 72, '2026-10-22T17:00:00Z', 7470468);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:20:35Z', 72, '2026-10-22T17:00:00Z', 7477407);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:24:38Z', 73, '2026-10-22T17:00:00Z', 7503696);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:25:30Z', 73, '2026-10-22T17:00:00Z', 7517240);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:29:40Z', 73, '2026-10-22T17:00:00Z', 7518508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:36:30Z', 74, '2026-10-22T17:00:00Z', 7790632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:36:45Z', 74, '2026-10-22T17:00:00Z', 7790632);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:40:56Z', 74, '2026-10-22T17:00:00Z', 7793132);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:45:40Z', 75, '2026-10-22T17:00:00Z', 8067622);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:46:30Z', 75, '2026-10-22T17:00:00Z', 8075893);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:50:49Z', 75, '2026-10-22T17:00:00Z', 8088667);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:58:11Z', 77, '2026-10-22T17:00:00Z', 8378038);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T15:58:30Z', 77, '2026-10-22T17:00:00Z', 8378038);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:02:35Z', 78, '2026-10-22T17:00:00Z', 8626757);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:04:47Z', 78, '2026-10-22T17:00:00Z', 8696535);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:05:27Z', 78, '2026-10-22T17:00:00Z', 8710336);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:09:39Z', 79, '2026-10-22T17:00:00Z', 8784985);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:10:21Z', 79, '2026-10-22T17:00:00Z', 9063345);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:12:21Z', 80, '2026-10-22T17:00:00Z', 9095910);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:16:27Z', 81, '2026-10-22T17:00:00Z', 9149928);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:19:30Z', 81, '2026-10-22T17:00:00Z', 9419560);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:22:21Z', 82, '2026-10-22T17:00:00Z', 9633267);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:27:00Z', 0, '2026-10-22T21:20:00Z', 221381);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:27:05Z', 1, '2026-10-22T21:20:00Z', 223392);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:32:30Z', 2, '2026-10-22T21:20:00Z', 571351);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:35:01Z', 3, '2026-10-22T21:20:00Z', 666117);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:39:21Z', 4, '2026-10-22T21:20:00Z', 865570);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:43:26Z', 5, '2026-10-22T21:20:00Z', 963567);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:47:30Z', 5, '2026-10-22T21:20:00Z', 1015034);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:50:53Z', 6, '2026-10-22T21:20:00Z', 1094929);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:54:30Z', 7, '2026-10-22T21:20:00Z', 1208035);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T16:57:24Z', 8, '2026-10-22T21:20:00Z', 1572873);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:01:52Z', 9, '2026-10-22T21:20:00Z', 1724582);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:03:34Z', 10, '2026-10-22T21:20:00Z', 1931508);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:04:36Z', 11, '2026-10-22T21:20:00Z', 2318395);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:04:57Z', 12, '2026-10-22T21:20:00Z', 2402832);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:05:51Z', 13, '2026-10-22T21:20:00Z', 2553947);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:06:38Z', 14, '2026-10-22T21:20:00Z', 2670483);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:07:41Z', 15, '2026-10-22T21:20:00Z', 2835128);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:08:34Z', 16, '2026-10-22T21:20:00Z', 3004593);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:09:18Z', 17, '2026-10-22T21:20:00Z', 3092775);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:09:29Z', 17, '2026-10-22T21:20:00Z', 3119406);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:10:32Z', 18, '2026-10-22T21:20:00Z', 3168477);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:12:35Z', 19, '2026-10-22T21:20:00Z', 3262137);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:15:35Z', 20, '2026-10-22T21:20:00Z', 3420513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:16:19Z', 21, '2026-10-22T21:20:00Z', 3759364);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:16:25Z', 22, '2026-10-22T21:20:00Z', 3786317);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:17:32Z', 23, '2026-10-22T21:20:00Z', 4027027);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:17:41Z', 24, '2026-10-22T21:20:00Z', 4065566);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:18:32Z', 24, '2026-10-22T21:20:00Z', 4117015);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:19:35Z', 25, '2026-10-22T21:20:00Z', 4203108);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:20:35Z', 26, '2026-10-22T21:20:00Z', 4261464);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:21:39Z', 27, '2026-10-22T21:20:00Z', 4321067);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:24:34Z', 28, '2026-10-22T21:20:00Z', 4386690);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:27:14Z', 29, '2026-10-22T21:20:00Z', 4450083);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:27:24Z', 29, '2026-10-22T21:20:00Z', 4453268);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:29:11Z', 30, '2026-10-22T21:20:00Z', 4550465);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:30:35Z', 31, '2026-10-22T21:20:00Z', 4735192);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:31:54Z', 32, '2026-10-22T21:20:00Z', 4841181);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:32:32Z', 33, '2026-10-22T21:20:00Z', 5176341);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:36:21Z', 34, '2026-10-22T21:20:00Z', 5220961);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:40:33Z', 35, '2026-10-22T21:20:00Z', 5279570);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:41:35Z', 35, '2026-10-22T21:20:00Z', 5315118);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:42:32Z', 36, '2026-10-22T21:20:00Z', 5658513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:46:38Z', 37, '2026-10-22T21:20:00Z', 5703348);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:47:04Z', 37, '2026-10-22T21:20:00Z', 5755173);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:48:35Z', 38, '2026-10-22T21:20:00Z', 6015187);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:49:34Z', 39, '2026-10-22T21:20:00Z', 6089442);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:50:41Z', 40, '2026-10-22T21:20:00Z', 6155997);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:52:32Z', 41, '2026-10-22T21:20:00Z', 6531339);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:52:33Z', 42, '2026-10-22T21:20:00Z', 6531339);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:55:10Z', 42, '2026-10-22T21:20:00Z', 6570911);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T17:59:13Z', 42, '2026-10-22T21:20:00Z', 6572232);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:03:09Z', 44, '2026-10-22T21:20:00Z', 6926548);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:07:12Z', 45, '2026-10-22T21:20:00Z', 6964194);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:10:38Z', 45, '2026-10-22T21:20:00Z', 6968343);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:13:34Z', 46, '2026-10-22T21:20:00Z', 7229221);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:14:16Z', 47, '2026-10-22T21:20:00Z', 7529784);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:14:22Z', 48, '2026-10-22T21:20:00Z', 7922731);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:14:32Z', 49, '2026-10-22T21:20:00Z', 8026762);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:15:32Z', 50, '2026-10-22T21:20:00Z', 8172749);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:17:32Z', 51, '2026-10-22T21:20:00Z', 8314399);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:18:32Z', 52, '2026-10-22T21:20:00Z', 8388252);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:21:32Z', 53, '2026-10-22T21:20:00Z', 8501529);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:21:33Z', 53, '2026-10-22T21:20:00Z', 8501529);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:23:48Z', 54, '2026-10-22T21:20:00Z', 8536408);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:25:32Z', 55, '2026-10-22T21:20:00Z', 8622815);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:27:02Z', 56, '2026-10-22T21:20:00Z', 8742161);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:28:32Z', 57, '2026-10-22T21:20:00Z', 8868327);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:29:32Z', 58, '2026-10-22T21:20:00Z', 8912207);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:31:32Z', 59, '2026-10-22T21:20:00Z', 9011805);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:31:35Z', 59, '2026-10-22T21:20:00Z', 9014966);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:34:24Z', 60, '2026-10-22T21:20:00Z', 9076310);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:35:32Z', 61, '2026-10-22T21:20:00Z', 9166601);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:37:32Z', 62, '2026-10-22T21:20:00Z', 9203591);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:39:32Z', 63, '2026-10-22T21:20:00Z', 9378660);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:41:32Z', 64, '2026-10-22T21:20:00Z', 9422982);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:43:32Z', 65, '2026-10-22T21:20:00Z', 9486475);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:43:41Z', 65, '2026-10-22T21:20:00Z', 9489541);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:46:32Z', 66, '2026-10-22T21:20:00Z', 9564029);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:48:32Z', 67, '2026-10-22T21:20:00Z', 9599778);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:51:32Z', 68, '2026-10-22T21:20:00Z', 9676186);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:53:32Z', 69, '2026-10-22T21:20:00Z', 9709629);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:55:32Z', 70, '2026-10-22T21:20:00Z', 9770190);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T18:58:32Z', 71, '2026-10-22T21:20:00Z', 9820476);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:01:32Z', 72, '2026-10-22T21:20:00Z', 9886557);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:01:40Z', 72, '2026-10-22T21:20:00Z', 9886557);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:04:32Z', 73, '2026-10-22T21:20:00Z', 9935056);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:07:17Z', 74, '2026-10-22T21:20:00Z', 9994555);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:11:26Z', 75, '2026-10-22T21:20:00Z', 10055235);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:14:55Z', 76, '2026-10-22T21:20:00Z', 10113027);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:19:02Z', 77, '2026-10-22T21:20:00Z', 10179815);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:23:05Z', 78, '2026-10-22T21:20:00Z', 10254085);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:23:32Z', 78, '2026-10-22T21:20:00Z', 10254085);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:27:43Z', 79, '2026-10-22T21:20:00Z', 10312585);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:30:53Z', 79, '2026-10-22T21:20:00Z', 10350514);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:35:04Z', 80, '2026-10-22T21:20:00Z', 10397885);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:37:32Z', 80, '2026-10-22T21:20:00Z', 10445251);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:40:32Z', 81, '2026-10-22T21:20:00Z', 10502559);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:44:38Z', 82, '2026-10-22T21:20:00Z', 10582923);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:46:32Z', 82, '2026-10-22T21:20:00Z', 10617219);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:50:36Z', 83, '2026-10-22T21:20:00Z', 10683190);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:52:32Z', 83, '2026-10-22T21:20:00Z', 10702963);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:56:37Z', 84, '2026-10-22T21:20:00Z', 10772873);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T19:57:35Z', 84, '2026-10-22T21:20:00Z', 10787162);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:01:36Z', 85, '2026-10-22T21:20:00Z', 10848891);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:03:32Z', 85, '2026-10-22T21:20:00Z', 10884445);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:07:32Z', 86, '2026-10-22T21:20:00Z', 10949301);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:11:38Z', 87, '2026-10-22T21:20:00Z', 11012372);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:12:27Z', 87, '2026-10-22T21:20:00Z', 11015413);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:16:37Z', 88, '2026-10-22T21:20:00Z', 11084009);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:17:30Z', 88, '2026-10-22T21:20:00Z', 11089670);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:21:46Z', 89, '2026-10-22T21:20:00Z', 11168383);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:22:32Z', 89, '2026-10-22T21:20:00Z', 11181706);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:26:41Z', 90, '2026-10-22T21:20:00Z', 11237560);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:28:32Z', 90, '2026-10-22T21:20:00Z', 11251359);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:32:42Z', 91, '2026-10-22T21:20:00Z', 11321749);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:33:32Z', 91, '2026-10-22T21:20:00Z', 11326047);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:37:40Z', 92, '2026-10-22T21:20:00Z', 11376321);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:39:32Z', 92, '2026-10-22T21:20:00Z', 11388494);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:43:36Z', 93, '2026-10-22T21:20:00Z', 11484055);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:44:32Z', 93, '2026-10-22T21:20:00Z', 11497384);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:48:37Z', 94, '2026-10-22T21:20:00Z', 11529738);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:51:32Z', 94, '2026-10-22T21:20:00Z', 11547437);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:55:32Z', 95, '2026-10-22T21:20:00Z', 11610194);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T20:59:34Z', 96, '2026-10-22T21:20:00Z', 11640513);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:03:41Z', 96, '2026-10-22T21:20:00Z', 11677028);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:04:32Z', 96, '2026-10-22T21:20:00Z', 11679974);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:08:34Z', 96, '2026-10-22T21:20:00Z', 11706131);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:11:33Z', 97, '2026-10-22T21:20:00Z', 11743875);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:15:41Z', 97, '2026-10-22T21:20:00Z', 11768478);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:20:19Z', 0, '2026-10-23T02:20:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:20:39Z', 0, '2026-10-23T02:20:00Z', 16666);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:21:32Z', 1, '2026-10-23T02:20:00Z', 226134);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:21:51Z', 2, '2026-10-23T02:20:00Z', 508971);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:26:03Z', 3, '2026-10-23T02:20:00Z', 688094);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:26:26Z', 3, '2026-10-23T02:20:00Z', 695025);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:30:38Z', 4, '2026-10-23T02:20:00Z', 836687);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:30:57Z', 4, '2026-10-23T02:20:00Z', 861509);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:32:32Z', 5, '2026-10-23T02:20:00Z', 1037530);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:34:05Z', 6, '2026-10-23T02:20:00Z', 1158022);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:35:32Z', 7, '2026-10-23T02:20:00Z', 1242783);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:37:16Z', 8, '2026-10-23T02:20:00Z', 1340688);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:39:22Z', 9, '2026-10-23T02:20:00Z', 1424788);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:41:15Z', 10, '2026-10-23T02:20:00Z', 1508108);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:43:24Z', 11, '2026-10-23T02:20:00Z', 1599295);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:43:27Z', 11, '2026-10-23T02:20:00Z', 1601967);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:45:30Z', 12, '2026-10-23T02:20:00Z', 1657701);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:49:01Z', 13, '2026-10-23T02:20:00Z', 1736746);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:53:05Z', 14, '2026-10-23T02:20:00Z', 1785959);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:54:38Z', 14, '2026-10-23T02:20:00Z', 1796749);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T21:58:57Z', 15, '2026-10-23T02:20:00Z', 1846880);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-22T22:03:10Z', 15, '2026-10-23T02:20:00Z', 1854201);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:13:12Z', 2, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:17:46Z', 2, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:22:11Z', 2, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:27:24Z', 2, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:31:29Z', 2, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:36:07Z', 2, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:40:20Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:44:40Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:48:44Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:52:50Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T00:57:04Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:01:25Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:05:54Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:11:08Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:16:17Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:21:41Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:27:05Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:32:32Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:37:48Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:43:03Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T01:48:06Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:03:10Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:08:15Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:13:20Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:18:25Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:23:25Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:28:26Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:33:39Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:38:41Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:43:54Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:49:08Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:54:16Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T02:59:18Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:04:21Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:09:35Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:14:46Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:19:53Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:25:02Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:30:09Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:35:12Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:40:25Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:45:35Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:50:42Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T03:56:17Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:01:27Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:06:39Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:11:40Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:16:42Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:21:45Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:26:55Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:32:08Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:37:16Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:42:25Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:47:28Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:52:30Z', 3, '2026-10-24T05:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-24T04:57:39Z', 3, '2026-10-24T05:00:00Z', 0);

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
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T06:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T01:30:00Z', 'Ok', 'ended at 96%', 96, 96, 15447295, 142, 0, NULL, NULL, 2266, 681.9, 27, 0, 12, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T11:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T06:30:00Z', 'Ok', 'ended at 97%', 97, 97, 15382734, 128, 0, NULL, NULL, 1916, 1181.7, 49, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T16:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T11:30:00Z', 'Under', 'ended at 53%', 53, 53, 8348667, 132, 0, NULL, NULL, 762, 191, 6, 3, 20, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T18:30:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T13:30:00Z', 'Ok', 'ended at 96%', 96, 96, 11561646, 127, 0, NULL, NULL, 529, 303, 2, 3, 10, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T19:00:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T14:00:00Z', 'Under', 'ended at 0%', 0, 0, 2141, 1, 0, NULL, NULL, 793, 408.7, 2, 3, 7, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-21T23:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T18:40:00Z', 'Under', 'ended at 87%', 87, 87, 9713944, 116, 0, NULL, NULL, 1058, 218, 1, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-22T04:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-21T23:40:00Z', 'Ok', 'ended at 98%', 98, 98, 14002227, 139, 0, NULL, NULL, 1636, 678.2, 25, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-22T09:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T04:40:00Z', 'Ok', 'ended at 96%', 96, 96, 12282615, 133, 0, NULL, NULL, 1994, 2314.5, 14, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-22T14:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T09:40:00Z', 'Under', 'ended at 60%', 60, 60, 9516842, 164, 0, NULL, NULL, 1561, 297, 12, 0, 7, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-22T17:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T12:00:00Z', 'Under', 'ended at 82%', 82, 82, 9633267, 111, 0, NULL, NULL, 608, 52.6, 0, 0, 5, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-22T21:20:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T16:20:00Z', 'Ok', 'ended at 97%', 97, 97, 11768478, 129, 0, NULL, NULL, 1189, 1571.9, 17, 4, 3, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T00:20:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T19:20:00Z', 'Ok', 'ended at 95%', 95, 95, 12124039, 98, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T00:30:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T19:30:00Z', 'Blind', 'Claude''s meter unseen for 1h while 191.7k were spent', 2, 2, 489533, 11, 0, NULL, NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-22T19:39:01Z","ToUtc":"2026-10-22T21:03:22Z","Fresh":190960,"Length":"01:24:21"},{"FromUtc":"2026-10-22T21:26:19Z","ToUtc":"2026-10-22T21:49:00Z","Fresh":708,"Length":"00:22:41"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T02:20:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T21:20:00Z', 'Under', 'ended at 15%', 15, 15, 1854201, 22, 0, NULL, NULL, 2083, 355.4, 2, 2, 5, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T04:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/0df2b9d7-2637-40ea-bac8-2354ec84d894', '2026-10-22T23:20:00Z', 'Under', 'ended at 0%', 0, 0, 0, 1, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T04:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T23:20:00Z', 'Under', 'ended at 0%', 0, 0, 0, 1, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T04:20:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-22T23:20:00Z', 'Under', 'ended at 0%', 0, 0, 0, 1, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T05:20:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T00:20:00Z', 'Under', 'ended at 93%', 93, 93, 12178250, 108, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T10:20:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T05:20:00Z', 'Blind', 'Claude''s meter unseen for 25m while 1.3M were spent', 93, 93, 14018327, 91, 0, NULL, NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-23T07:38:35Z","ToUtc":"2026-10-23T08:04:31Z","Fresh":1334096,"Length":"00:25:56"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T15:20:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T10:20:00Z', 'Ok', 'ended at 97%', 97, 97, 18619702, 250, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T19:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/0df2b9d7-2637-40ea-bac8-2354ec84d894', '2026-10-23T14:20:00Z', 'Under', 'ended at 73%', 73, 73, 8797979, 99, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T19:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T14:20:00Z', 'Under', 'ended at 73%', 73, 73, 8797979, 99, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T19:40:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T14:40:00Z', 'Under', 'ended at 0%', 0, 0, 765776, 37, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T21:10:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T16:10:00Z', 'Under', 'ended at 74%', 74, 74, 8340576, 96, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-23T21:10:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T16:10:00Z', 'Ok', 'ended at 98%', 98, 98, 12517773, 105, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T00:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/0df2b9d7-2637-40ea-bac8-2354ec84d894', '2026-10-23T19:20:00Z', 'Under', 'ended at 61%', 61, 61, 9290638, 98, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T00:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T19:20:00Z', 'Under', 'ended at 61%', 61, 61, 9290638, 98, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T03:00:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T22:00:00Z', 'Under', 'ended at 71%', 71, 71, 8243820, 101, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T03:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-23T22:00:00Z', 'Over', 'Claude reported 100%', 100, 100, 14747323, 111, 79.9, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T05:00:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T00:00:00Z', 'Under', 'ended at 3%', 3, 3, 0, 56, 0, NULL, NULL, 4, 1.9, 19, 35, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T05:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/0df2b9d7-2637-40ea-bac8-2354ec84d894', '2026-10-24T00:20:00Z', 'Ok', 'ended at 98%', 98, 98, 12464956, 77, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T05:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T00:20:00Z', 'Ok', 'ended at 98%', 98, 98, 12464956, 77, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T08:00:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T03:00:00Z', 'Under', 'ended at 3%', 3, 3, 120409, 61, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T08:20:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T03:20:00Z', 'Under', 'ended at 46%', 46, 46, 8521145, 79, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T08:50:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T03:50:00Z', 'Under', 'ended at 0%', 0, 0, 0, 58, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T15:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T10:20:00Z', 'Blind', 'Claude''s meter unseen for 46m while 789.1k were spent', 20, 20, 1074919, 3, 0, NULL, NULL, 5, 2.3, 1, 0, 1, '[{"FromUtc":"2026-10-24T10:20:00Z","ToUtc":"2026-10-24T11:06:22Z","Fresh":789051,"Length":"00:46:22"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T15:20:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T10:20:00Z', 'Ok', 'ended at 95%', 95, 95, 11324180, 109, 0, NULL, NULL, 4, 12.2, 2, 2, 2, '[]');

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
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', '(workspace root)', 'Claude', NULL, NULL, 8851134);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'Gadfly', 'Claude', NULL, NULL, 760750);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Browser', 'Claude', NULL, NULL, 3450213);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Launcher', 'Claude', NULL, NULL, 2152001);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Legion', 'Claude', NULL, NULL, 1204623);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 2108888);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'Prose', 'Claude', NULL, NULL, 40224388);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-13', 'SyntheticExchangeTheory', 'Claude', NULL, NULL, 176265);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', '(workspace root)', 'Claude', NULL, NULL, 2419866);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'MindAttic.Launcher', 'Claude', NULL, NULL, 767669);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'MindAttic.Log', 'Claude', NULL, NULL, 1312534);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 6151618);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-14', 'Prose', 'Claude', NULL, NULL, 33752980);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', '(outside workspace)', 'Claude', NULL, NULL, 10769526);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', '(workspace root)', 'Claude', NULL, NULL, 4095244);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'AutoWebNav', 'Claude', NULL, NULL, 135009);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Automata', 'Claude', NULL, NULL, 922326);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Cursory', 'Claude', NULL, NULL, 99804);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Formicarium', 'Claude', NULL, NULL, 144481);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GTFO.EZ', 'Claude', NULL, NULL, 2283733);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'GTFO.SkipDrop', 'Claude', NULL, NULL, 249611);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Gadfly', 'Claude', NULL, NULL, 84653);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MediaButler', 'Claude', NULL, NULL, 258891);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Authentication', 'Claude', NULL, NULL, 246740);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Capture', 'Claude', NULL, NULL, 622827);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Deploy', 'Claude', NULL, NULL, 94337);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Ideas', 'Claude', NULL, NULL, 162693);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Launcher', 'Claude', NULL, NULL, 109561);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Legion', 'Claude', NULL, NULL, 64505);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Log', 'Claude', NULL, NULL, 259598);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Media', 'Claude', NULL, NULL, 58402);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 6147506);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Psst', 'Claude', NULL, NULL, 45057);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Telemetry', 'Claude', NULL, NULL, 123716);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'MindAttic.Vault', 'Claude', NULL, NULL, 44565);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'OpenCredentials', 'Claude', NULL, NULL, 133990);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'Prose', 'Claude', NULL, NULL, 13252390);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'TaxRateCollector', 'Claude', NULL, NULL, 111114);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-15', 'UnityRonin', 'Claude', NULL, NULL, 78248);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', '(workspace root)', 'Claude', NULL, NULL, 34362372);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GTFO.CrashTestDummies', 'Claude', NULL, NULL, 719758);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GTFO.EZ', 'Claude', NULL, NULL, 248521);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'GTFO.SkipDrop', 'Claude', NULL, NULL, 131362);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'MindAttic.Capture', 'Claude', NULL, NULL, 587628);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'MindAttic.Launcher', 'Claude', NULL, NULL, 452413);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 3254400);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'Prose', 'Claude', NULL, NULL, 5381163);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-16', 'Unstuck', 'Claude', NULL, NULL, 1445263);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', '(workspace root)', 'Claude', NULL, NULL, 20743931);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2956240);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.CrashTestDummies', 'Claude', NULL, NULL, 437119);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.EZ', 'Claude', NULL, NULL, 137002);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.EZ', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1422405);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'GTFO.SkipDrop', 'Claude', NULL, NULL, 83140);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Browser', 'Claude', NULL, NULL, 612946);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Capture', 'Claude', NULL, NULL, 676337);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Launcher', 'Claude', NULL, NULL, 100032);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Orchestrator', 'Claude', NULL, NULL, 16894404);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3210792);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'MindAttic.Vault', 'Claude', NULL, NULL, 129138);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'Prose', 'Claude', NULL, NULL, 4275346);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-17', 'SyntheticExchangeTheory', 'Claude', NULL, NULL, 136464);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 14220459);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'GTFO.CrashTestDummies', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 683316);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'GTFO.EZ', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 6936509);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'MindAttic.Export', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 39539);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 34694995);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-18', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3381774);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4135189);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 31559231);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', 'billed:api-key', NULL, 110945);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 319892);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1249890);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4283181);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ.Shared', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1692);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'GTFO.EZ.Shared', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 203113);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'MindAttic.Orchestrator', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2096848);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2965008);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3482350);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'ProcessLogger', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 917453);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'ProcessLogger', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 105732);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'Prose', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 91497);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 689564);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16670601);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T22:00:00Z', 15177387);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'GTFO.EZ', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T22:00:00Z', 4807881);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Launcher', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 186545);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Legion', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 128440);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Legion', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T22:00:00Z', 155909);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3995599);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-20T22:00:00Z', 19929280);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Tickets', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 741844);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Vault', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 26963);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'Unity.Cursory', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18331);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'Unity.GridGame', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 158532);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'Unity.Multiplayer', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 458362);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'Unity.SpaceGod', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 510414);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', '(workspace root)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9617143);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1278018);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 109424);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'AutoWebNav', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15938);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Automata', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16698);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Cursory', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 19526);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Cursory', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 24861);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Formicarium', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15443);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'IdiotProof', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18393);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'JellyFinPoster', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15373);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'JobHunt', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16081);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'KdpPublish', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16076);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MediaButler', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16971);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Authentication', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17982);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Browser', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15783);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Capture', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15339);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Deploy', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18782);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Export', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15011);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Helpers', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17551);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Ideas', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17791);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Launcher', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18910);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Legion', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 19353);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Log', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15478);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Media', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15408);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Orchestrator', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3777622);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Orchestrator', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 10826);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 61587);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 57420);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Psst', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18693);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Vault', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18830);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'MindAttic.Web', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15879);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'OpenCredentials', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18014);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'SyntheticExchangeTheory', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15172);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Tutor', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 18296);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.Cursory', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 13201979);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.Cursory', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 745489);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.GridGame', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2169343);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.GridGame', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4269446);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.Multiplayer', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 5170494);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.SpaceGod', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 8626927);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'Unity.SpaceGod', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 14262591);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-21', 'UnityRonin', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15219);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(workspace root)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9397252);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3375720);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1782588);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'ChiMesh', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 489103);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Claudia', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 64078);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Capture', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 90056);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Launcher', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 446040);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Orchestrator', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1176036);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2600129);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'OpenCredentials', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 32416);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'REA.Bloodlines', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9056223);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'REA.Bloodlines', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7522780);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'REA.Bloodlines', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7770642);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Chain', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 22858);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Cursory', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2689500);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Cursory', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 21886);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.GridGame', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2826980);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Hare', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 21378);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Incognito', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 267776);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Multiplayer', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 23184);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.QuickDuel', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 22496);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Ricochet', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 63262);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.SpaceGod', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7317248);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.SpaceGod', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 30707);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.SpaceGod', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9681293);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 538389);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', 'billed:api-key', NULL, 690741);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3754319);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Agency', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 19649);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Launcher', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 94860);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Orchestrator', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 20624973);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3477982);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Orchestrator', 'Claude', 'billed:api-key', NULL, 98677);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Tickets', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 19492);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Prose', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1534514);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'REA.Bloodlines', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 11288162);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'REA.Bloodlines', 'Claude', 'billed:api-key', NULL, 6599351);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Chain', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1356130);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Chain', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 323045);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Cursory', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 8498);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Hare', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 76177);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Hare', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1813806);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Hare', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 431198);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Incognito', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1890);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Incognito', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 54015);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Multiplayer', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1250923);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Overworld', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 120574);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.ProjectVania', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 188882);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.QuickDuel', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1156822);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.QuickDuel', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 53121);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Ricochet', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3357755);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Ricochet', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 354263);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.SpaceGod', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9334947);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.SpaceGod', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17621786);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.SpaceGod', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 25808765);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(workspace root)', 'Claude', 'billed:api-key', NULL, 72900);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7515129);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'GTFO.EZ.AbsurdWeapons', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1107343);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Agency', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 898849);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Agency', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 90214);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Launcher', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 300402);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4658634);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 27651);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', 'billed:api-key', NULL, 4616053);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 574609);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Tickets', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 93780);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'REA.Bloodlines', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 10524965);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Chain', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 84481);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Chain', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2175300);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Hare', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 254787);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Hare', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 197112);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Incognito', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3458469);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Incognito', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 241143);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Overworld', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 320620);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Overworld', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 123170);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.ProjectVania', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3557510);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.ProjectVania', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 63779);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.QuickDuel', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3028883);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.Ricochet', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 51148);

-- ticket_costs
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('00437c54-780b-4060-a0d9-d212cfac52c0', 'M.ORCH-T794', 'fix', 'MindAttic.Orchestrator', 5118, '2026-10-24T17:35:04.649Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('004c0693-b78e-4101-8c5a-4d4bb6911a1b', 'M.LNCH-T15', 'chore', 'MindAttic.Launcher', 27359, '2026-10-23T19:17:50.046Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('00cd01a1-e7b4-4a13-bc77-fe92abe508fa', 'M.ORCH-T706', 'fix', 'MindAttic.Orchestrator', 174680, '2026-10-23T20:22:50.564Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a15949cf958ac2769', 'UM-T19', 'docs', 'Unity.Multiplayer', 321140, '2026-10-21T10:18:27.895Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a1e9779d09d2d12a6', 'UM-T36', 'chore', 'Unity.Multiplayer', 1030156, '2026-10-21T08:13:12.056Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a4015776ebbfb48f9', 'UM-T29', 'feature', 'Unity.Multiplayer', 536051, '2026-10-21T07:07:56.064Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a6586bed4f1c7887c', 'UM-T30', 'feature', 'Unity.Multiplayer', 580368, '2026-10-21T07:50:50.495Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a6f9faba3a156eba5', 'UM-T27', 'feature', 'Unity.Multiplayer', 613515, '2026-10-21T06:17:20.71Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a7bf891439d724c47', 'UM-T28', 'feature', 'Unity.Multiplayer', 321037, '2026-10-21T07:00:59.21Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a91119caf59479c75', 'UM-T20', 'chore', 'Unity.Multiplayer', 169499, '2026-10-21T12:31:06.777Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-a991b88d589eb3919', 'UM-T26', 'feature', 'Unity.Multiplayer', 439995, '2026-10-21T05:59:56.942Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-ab42accab65a520ef', 'UM-T43', 'fix', 'Unity.Multiplayer', 130003, '2026-10-21T08:40:32.851Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-abd3948814dcd214c', 'UM-T18', 'docs', 'Unity.Multiplayer', 138475, '2026-10-21T08:35:34.005Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0108d19d-53e2-4bdc-88fc-d7459918ee0a/agent-ac4da8efa6a4c198f', 'UM-T31', 'feature', 'Unity.Multiplayer', 469959, '2026-10-21T08:11:06.427Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('010e2c71-f77b-493b-b09f-10a70efa0a5d', 'GG-T27', 'chore', 'GridGameGaiden', 91808, '2026-10-15T11:18:11.352Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('017d144b-2d43-47eb-823b-6b8c6b0b390e', 'M.ORCH-T788', 'fix', 'MindAttic.Orchestrator', 10617, '2026-10-24T16:40:47.044Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('01bbc686-02bf-47a3-b4be-70c6078238a7', 'UNI-T19', 'fix', 'Unity.Chain', 127237, '2026-10-24T17:01:14.458Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('01da70bc-b97a-412f-9004-0f594715b508', 'GE-T10', 'feature', 'GTFO.EZ', 178991, '2026-10-15T09:15:50.031Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('01fdfb78-8ed1-4a74-9776-cd4e949670ce', 'U.INC-T27', 'chore', 'Unity.Incognito', 35326, '2026-10-24T14:59:55.525Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a05f65bc714b46d40', 'M.ORCH-T372', 'fix', 'MindAttic.Orchestrator', 88047, '2026-10-18T08:07:00.191Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a109bad33834a052d', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 470437, '2026-10-18T05:43:24.301Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a21a55039a64af479', 'M.ORCH-T386', 'fix', 'MindAttic.Orchestrator', 34034, '2026-10-18T08:03:30.589Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a2af32f568f74309f', 'M.ORCH-T374', 'chore', 'MindAttic.Orchestrator', 123082, '2026-10-18T08:01:30.247Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a2c7f3af2d5c64da1', 'M.ORCH-T320', 'fix', 'MindAttic.Orchestrator', 74251, '2026-10-18T05:59:52.013Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a300889420223522f', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 743399, '2026-10-18T07:25:03.324Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a308c21d98bd12182', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 458277, '2026-10-18T10:35:31.422Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a323b666d2c43d956', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 2370505, '2026-10-18T10:12:39.306Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a32e1293e3e11e9f8', 'M.ORCH-T389', 'fix', 'MindAttic.Orchestrator', 52383, '2026-10-18T08:23:05.289Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a39a10707569bf892', 'M.ORCH-T284', 'fix', 'MindAttic.Orchestrator', 64515, '2026-10-18T02:17:14.658Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a4237d1a9b122ab8d', 'M.ORCH-T385', 'fix', 'MindAttic.Orchestrator', 184725, '2026-10-18T08:07:27.665Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a549a4d2e99478bb9', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 266801, '2026-10-18T10:58:48.206Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-a83b5bd29c0fb4308', 'M.ORCH-T384', 'fix', 'MindAttic.Orchestrator', 162632, '2026-10-18T08:15:39.15Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-aa85b48a671ad3c60', 'M.ORCH-T373', 'chore', 'MindAttic.Orchestrator', 60764, '2026-10-18T07:54:25.482Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-aaf74d2482caa969d', 'M.ORCH-T256', 'feature', 'MindAttic.Orchestrator', 929480, '2026-10-18T08:42:12.979Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-ab7bef213271a31a3', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 238536, '2026-10-18T07:51:21.476Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-ab9bcf96854c66e56', 'M.ORCH-T234', 'feature', 'MindAttic.Orchestrator', 491427, '2026-10-18T09:07:06.263Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-abf2ee2dfd56739d5', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 322044, '2026-10-18T11:28:56.044Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-ad5b6f9414ec8eb67', 'M.ORCH-T375', 'fix', 'MindAttic.Orchestrator', 201827, '2026-10-18T08:08:10.537Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('020dcdb4-a5ba-428f-aa48-d8f9c07c2dd3/agent-afa2c1c389bb61df3', 'M.ORCH-T388', 'fix', 'MindAttic.Orchestrator', 165698, '2026-10-18T08:31:30.981Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('02890e56-5257-49dd-8659-245102519bfa', 'GE-T3', 'chore', 'GTFO.EZ', 1749292, '2026-10-15T09:45:02.94Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('02cca644-9a1f-4424-813d-edfe444baa65', 'SS-T88', 'chore', 'Prose', 373434, '2026-10-15T20:48:59.345Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('030e5a5e-44f3-4b5a-a229-4d9707cf5903', 'GG-T21', 'chore', 'GridGameGaiden', 52863, '2026-10-15T11:13:00.014Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('034a58c5-8a10-4f41-9766-506a3cce90e4', 'USS-T39', 'feature', 'UnitySolarSystem', 290571, '2026-10-15T11:18:48.065Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('03ddf508-a736-4d57-a26c-3350b33e18b1', 'SS-T38', 'chore', 'Prose', 245228, '2026-10-15T12:54:52.624Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('04815c10-9d98-447d-a364-6fdbdc337e70', 'M.ORCH-T704', 'fix', 'MindAttic.Orchestrator', 20193, '2026-10-23T19:54:59.584Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('053545c4-2407-4c70-b0d0-4f1a0533e4e8', 'GG-T18', 'feature', 'GridGameGaiden', 27421, '2026-10-15T13:12:27.381Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('05637a82-0d68-46c4-b77c-798a6a0c30f6', 'AUT-T11', 'chore', 'Automata', 62425, '2026-10-16T00:56:27.827Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0657de29-2ef7-498f-b99b-937d9deef28b', 'MAO-T84', 'feature', 'MindAttic.Orchestrator', 749664, '2026-10-15T08:18:31.745Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0687c7fd-a751-4729-a87c-00812f3d32e9', 'M.CAPT-T2', 'chore', 'MindAttic.Capture', 551651, '2026-10-16T02:05:24.47Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('073cd0ba-725b-4465-9f0f-71fd9c93a2aa', 'M.ORCH-T653', 'fix', 'MindAttic.Orchestrator', 8056, '2026-10-23T01:40:59.199Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('090436c7-c731-48fd-8e1d-23070d467dca', 'U.RON-T2', 'chore', 'UnityRonin', 78248, '2026-10-16T01:30:25.95Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('098daffe-3b0a-4f04-a52e-bc8c0d2ef0ad', 'M.WEB-T10', 'docs', 'MindAttic.Web', 15879, '2026-10-21T18:10:42.43Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-a3cfe9c237caed6b0', 'G.EZ-T26', 'chore', 'GTFO.EZ', 404945, '2026-10-18T03:03:31.362Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-a8a88167f7ced5ec9', 'G.EZ-T28', 'chore', 'GTFO.EZ', 188662, '2026-10-18T02:52:41.9Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-ae14e91077f74649e', 'G.EZ-T27', 'chore', 'GTFO.EZ', 31506, '2026-10-18T02:49:02.782Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-af704ff01b2a3d501', 'G.EZ-T29', 'docs', 'GTFO.EZ', 107781, '2026-10-18T02:51:43.736Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ce1d76e-5332-4e7c-b641-5307d28b8a5d', 'M.ORCH-T625', 'fix', 'MindAttic.Orchestrator', 19763, '2026-10-22T17:42:49.864Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0e3d9912-af74-4f05-8c25-2603c82585c1', 'M.ORCH-T732', 'fix', 'MindAttic.Orchestrator', 7443, '2026-10-23T20:25:52.192Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0e7342a0-81df-4f84-b4f2-fbda7f46a44b', 'M.ORCH-T600', 'fix', 'MindAttic.Orchestrator', 27086, '2026-10-22T00:33:57.917Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0f0deb42-1b30-43f2-ad51-ee05e2037c8a', 'SS-T95', 'chore', 'Prose', 129365, '2026-10-15T18:18:26.471Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0f5d1472-92bc-4241-9d95-fa6f19cd2d87', 'SS-T57', 'chore', 'Prose', 81168, '2026-10-15T12:44:26.548Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0f8387ba-fa84-4f28-b8cb-da38cf0a879d', 'M.ORCH-T740', 'fix', 'MindAttic.Orchestrator', 53732, '2026-10-23T20:26:06.771Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fd3fa21-5da2-43af-b334-1fef4f486b65', 'UQD-T29', 'chore', 'Unity.QuickDuel', 49234, '2026-10-24T11:40:54.613Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ff1d016-3c88-48f4-ac85-a3673d5c6dec', 'M.AUTH-T3', 'chore', 'MindAttic.Authentication', 121409, '2026-10-16T01:52:29.507Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('105d8ebd-4b74-44e9-82b6-85b7812d35fc', 'U.OVW-T2', 'chore', 'Unity.Overworld', 100252, '2026-10-23T22:29:26.119Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('107e3d68-dd6f-440e-a0ef-8ea144cf8f22', 'GGC-T3', 'chore', 'GridGameCaravan', 63008, '2026-10-16T01:40:45.331Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('114901c6-c933-45df-9236-c6705327bb43', 'UR-T65', 'chore', 'Unity.Ricochet', 21985, '2026-10-24T00:20:00.604Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a084b5be806617811', 'M.ORCH-T186', 'feature', 'MindAttic.Orchestrator', 37273, '2026-10-17T23:51:20.384Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a15fc697d5d9ec701', 'M.ORCH-T252', 'fix', 'MindAttic.Orchestrator', 105581, '2026-10-17T21:07:06.621Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a1a31220cb5c1c36f', 'M.ORCH-T178', 'fix', 'MindAttic.Orchestrator', 28272, '2026-10-17T16:07:18.954Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a1ce20af14ce2d27c', 'M.ORCH-T203', 'fix', 'MindAttic.Orchestrator', 114317, '2026-10-17T18:14:55.192Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a3c2a4fbc44fcd359', 'M.ORCH-T177', 'fix', 'MindAttic.Orchestrator', 15995, '2026-10-17T16:01:01.69Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a3e9fc2f2321b7f5a', 'M.ORCH-T174', 'chore', 'MindAttic.Orchestrator', 127590, '2026-10-18T00:23:11.976Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a42e976dd83f31c39', 'M.ORCH-T205', 'fix', 'MindAttic.Orchestrator', 196931, '2026-10-17T18:57:34.002Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a4b335b19ee13aea6', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 4102394, '2026-10-18T01:12:54.612Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a4c867dfc5367236e', 'M.ORCH-T255', 'fix', 'MindAttic.Orchestrator', 157502, '2026-10-17T23:39:15.816Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a5e4abfe2d14e5ef5', 'M.ORCH-T176', 'feature', 'MindAttic.Orchestrator', 109831, '2026-10-18T00:02:38.295Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a63ba8790d45af02d', 'M.ORCH-T196', 'fix', 'MindAttic.Orchestrator', 139906, '2026-10-17T16:34:18.844Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a700e8b0517ec706e', 'M.ORCH-T200', 'fix', 'MindAttic.Orchestrator', 178702, '2026-10-17T16:46:08.453Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a72b90568585bf074', 'M.ORCH-T175', 'docs', 'MindAttic.Orchestrator', 86016, '2026-10-17T16:03:33.029Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a7bdd8f57daba8ae6', 'M.ORCH-T189', 'chore', 'MindAttic.Orchestrator', 31642, '2026-10-18T00:21:04.369Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a83974bdc86d254e9', 'M.ORCH-T225', 'fix', 'MindAttic.Orchestrator', 58853, '2026-10-17T18:43:28.27Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a83f02ae65316f5e3', 'M.ORCH-T254', 'fix', 'MindAttic.Orchestrator', 87779, '2026-10-17T22:46:14.611Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-a95c791d67d1160d3', 'M.ORCH-T251', 'fix', 'MindAttic.Orchestrator', 65938, '2026-10-17T20:36:45.891Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aa8a7b4eb40d1989f', 'M.ORCH-T245', 'fix', 'MindAttic.Orchestrator', 301950, '2026-10-17T21:08:57.239Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aa967033ebff88f58', 'M.ORCH-T152', 'chore', 'MindAttic.Orchestrator', 50289, '2026-10-17T16:12:46.816Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aba9e38f249215e18', 'M.ORCH-T179', 'fix', 'MindAttic.Orchestrator', 81634, '2026-10-17T16:09:16.629Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-abb3046df73b9bfbd', 'M.ORCH-T194', 'fix', 'MindAttic.Orchestrator', 32422, '2026-10-17T16:18:51.135Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ac4b0ec5da035c58c', 'M.ORCH-T201', 'fix', 'MindAttic.Orchestrator', 49020, '2026-10-17T17:06:45.009Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ac5f335204b8becd8', 'M.ORCH-T188', 'fix', 'MindAttic.Orchestrator', 63750, '2026-10-17T16:21:50.378Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ac69d59c4460c404e', 'M.ORCH-T173', 'chore', 'MindAttic.Orchestrator', 15047, '2026-10-17T23:57:47.498Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-ada16f0b6497c485c', 'M.ORCH-T250', 'fix', 'MindAttic.Orchestrator', 36089, '2026-10-17T20:34:26.462Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aeb44daf4a03b8e28', 'M.ORCH-T151', 'chore', 'MindAttic.Orchestrator', 50057, '2026-10-17T16:01:43.181Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-aeb804e16a7670798', 'M.ORCH-T185', 'chore', 'MindAttic.Orchestrator', 46060, '2026-10-18T00:24:54.534Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-af8772854cdf2bc38', 'M.ORCH-T253', 'fix', 'MindAttic.Orchestrator', 41996, '2026-10-17T20:36:12.474Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11683d9e-4c60-4d4d-855b-9623f266d074/agent-af9f9027a9b08bdd9', 'M.ORCH-T198', 'feature', 'MindAttic.Orchestrator', 157077, '2026-10-17T15:53:41.744Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13cb820a-3dc3-4951-a193-e21d77771fe8', 'SS-T14', 'chore', 'Prose', 32733, '2026-10-15T11:58:04.131Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1404abba-0362-4878-8b72-c568ac5ce656', 'CUR-T15', 'docs', 'Cursory', 19526, '2026-10-21T18:11:13.839Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('140574bc-26c8-4d4d-92e1-c0012e600219', 'CUR-T2', 'chore', 'Cursory', 45531, '2026-10-16T00:54:58.889Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('15cf5108-874e-4e5e-91a5-366c125f93cb', 'PRS-T16', 'fix', 'Prose', 425165, '2026-10-16T07:27:38.953Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('15f72e02-ad83-4020-96f8-d3ce6aba82c7', 'OC-T20', 'docs', 'OpenCredentials', 18014, '2026-10-21T18:11:43.275Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('160bb34c-06aa-4d20-ae84-967580cf3179', 'SS-T17', 'chore', 'Prose', 317446, '2026-10-15T12:49:09.975Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('178cf8c5-938c-4e2c-83c0-01f3e3bb628b', 'U.INC-T25', 'chore', 'Unity.Incognito', 23744, '2026-10-24T13:25:52.415Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('17c76877-4c52-4335-b7d0-0e516a026985', 'GG-T50', 'feature', 'GridGameGaiden', 261741, '2026-10-15T16:19:18.202Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1836d17d-b4aa-44ce-9a05-806e4c289e7e', 'PRS-T15', 'fix', 'Prose', 214629, '2026-10-16T00:56:27.24Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('18646bb1-e071-4e3c-8f1d-d803c1ee7d8b', 'SS-T68', 'chore', 'Prose', 230431, '2026-10-15T18:39:44.203Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('193fa3a0-18d0-473a-b49e-683b2af22292', 'GG-T42', 'feature', 'GridGameGaiden', 225349, '2026-10-15T14:26:40.89Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('197e90de-626c-4306-a6c3-9d44e960f129', 'U.OVW-T6', 'chore', 'Unity.Overworld', 123170, '2026-10-24T10:31:13.999Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('199c75fe-813e-442a-9969-3a7d5767cfe2', 'M.ORCH-T684', 'fix', 'MindAttic.Orchestrator', 62822, '2026-10-23T17:44:59.576Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1aa75f1d-1e8e-444b-80ef-d5ada14a1c2e', 'M.LNCH-T36', 'docs', 'MindAttic.Launcher', 18910, '2026-10-21T18:11:51.513Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1aef465b-8a47-4a66-99e7-ad07c29a8e1a', 'UH-T40', 'chore', 'Unity.Hare', 188521, '2026-10-24T01:20:06.736Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1c7e3212-f7ba-4793-b876-327d14fd62a0', 'GG-T26', 'chore', 'GridGameGaiden', 84129, '2026-10-15T11:16:42.051Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a0062451dc22102f5', 'UQD-T57', 'fix', 'Unity.SpaceGod', 265769, '2026-10-24T01:57:29.266Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a015c5177a09aaf0e', 'UNI-T22', 'fix', 'Unity.SpaceGod', 90930, '2026-10-23T22:17:37.996Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a034987a04b91f036', 'U.SPC-T481', 'feature', 'Unity.SpaceGod', 606194, '2026-10-23T18:31:12.186Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a03dd68c8d4d746e5', 'U.VAN-T5', 'chore', 'Unity.SpaceGod', 187347, '2026-10-24T01:19:51.598Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a04c23adc3dd21de0', 'UR-T67', 'fix', 'Unity.SpaceGod', 40114, '2026-10-24T01:09:23.654Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a06b346779e991038', 'U.SPC-T427', 'feature', 'Unity.SpaceGod', 161582, '2026-10-23T16:53:54.232Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a08431d93dbc15600', 'UR-T64', 'fix', 'Unity.SpaceGod', 44225, '2026-10-24T01:09:06.867Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a09c889a4e2c6bcc0', 'UH-T38', 'docs', 'Unity.SpaceGod', 128517, '2026-10-24T00:11:12.272Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a0c19a4b772f349c6', 'U.SPC-T493', 'fix', 'Unity.SpaceGod', 49867, '2026-10-23T18:39:58.848Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a0d944d578cde0065', 'UNI-T35', 'fix', 'Unity.SpaceGod', 61395, '2026-10-23T22:38:38.026Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a0df5e14c3903a0d7', 'U.SPC-T465', 'feature', 'Unity.SpaceGod', 310320, '2026-10-23T20:16:13.333Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a191e897cd1eaa800', 'UNI-T29', 'docs', 'Unity.SpaceGod', 114692, '2026-10-23T22:34:40.099Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a1999cf38481b7d92', 'UH-T73', 'fix', 'Unity.SpaceGod', 35910, '2026-10-24T01:07:32.12Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a22e591844de9efdd', 'U.INC-T20', 'chore', 'Unity.SpaceGod', 64632, '2026-10-24T01:14:38.184Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a258d5342e0069215', 'UNI-T66', 'fix', 'Unity.SpaceGod', 68396, '2026-10-24T00:22:22.354Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a26075d276411486a', 'U.SPC-T456', 'feature', 'Unity.SpaceGod', 213354, '2026-10-23T17:17:59.679Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a294b2a0fa97ebc63', 'UNI-T37', 'feature', 'Unity.SpaceGod', 76977, '2026-10-24T01:41:00.66Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a295bbf435d6e8a5c', 'UR-T65', 'chore', 'Unity.SpaceGod', 208929, '2026-10-24T00:25:06.052Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a29ece70bddcca4f2', 'UH-T17', 'feature', 'Unity.SpaceGod', 213023, '2026-10-23T23:51:47.608Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a2c5a98c17e4067d3', 'UNI-T23', 'fix', 'Unity.SpaceGod', 52531, '2026-10-23T22:14:54.545Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a2c8738f270f9a8f2', 'UR-T20', 'fix', 'Unity.SpaceGod', 31932, '2026-10-23T22:11:58.164Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a2d7098b52114fa19', 'U.SPC-T462', 'fix', 'Unity.SpaceGod', 62871, '2026-10-23T16:57:21.092Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a31f829532db539de', 'UNI-T48', 'feature', 'Unity.SpaceGod', 302094, '2026-10-24T01:51:06.861Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a33c078864500059a', 'U.SPC-T471', 'docs', 'Unity.SpaceGod', 72549, '2026-10-23T17:23:38.468Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a349ea66667c6ac96', 'UNI-T33', 'feature', 'Unity.SpaceGod', 381137, '2026-10-24T00:05:39.322Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a35118277330569ae', 'U.SPC-T483', 'feature', 'Unity.SpaceGod', 226112, '2026-10-23T17:53:53.972Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a3ba27eeee5bec728', 'U.SPC-T429', 'feature', 'Unity.SpaceGod', 55728, '2026-10-23T16:46:13.749Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a3d50a47e874064e6', 'UNI-T36', 'feature', 'Unity.SpaceGod', 218864, '2026-10-24T01:18:41.972Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a401cb81955b1643e', 'UNI-T32', 'feature', 'Unity.SpaceGod', 540958, '2026-10-23T23:21:46.887Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a40f9fe548f9fa404', 'UNI-T44', 'fix', 'Unity.SpaceGod', 158807, '2026-10-23T22:53:46.88Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a4142e9f4a931d0ae', 'U.SPC-T504', 'fix', 'Unity.SpaceGod', 28918, '2026-10-23T19:10:55.114Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a42e8448f279ae99e', 'UNI-T30', 'chore', 'Unity.SpaceGod', 161213, '2026-10-23T22:45:12.756Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a476cc6d71140c56c', 'UH-T39', 'chore', 'Unity.SpaceGod', 462441, '2026-10-24T00:42:09.506Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a493234e799f232c9', 'U.VAN-T6', 'chore', 'Unity.SpaceGod', 175299, '2026-10-24T01:41:50.244Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a4a9d91f2794f67d6', 'U.SPC-T503', 'fix', 'Unity.SpaceGod', 61772, '2026-10-23T19:08:43.115Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a4e0bf2eaa49e16df', 'UNI-T24', 'fix', 'Unity.SpaceGod', 21669, '2026-10-23T22:10:00.046Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a508425262560a4db', 'U.SPC-T464', 'fix', 'Unity.SpaceGod', 37279, '2026-10-23T17:03:36.384Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a524aeb86d8af01bc', 'UH-T37', 'chore', 'Unity.SpaceGod', 89884, '2026-10-24T00:06:16.139Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a53653e7f85879fe4', 'U.INC-T19', 'chore', 'Unity.SpaceGod', 62936, '2026-10-24T01:10:36.589Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a56c7af55a6c94c34', 'UNI-T46', 'feature', 'Unity.SpaceGod', 157907, '2026-10-23T22:59:15.002Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a5c40e02a5bdd4d7a', 'UR-T22', 'fix', 'Unity.SpaceGod', 30648, '2026-10-23T22:16:05.09Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a5da4a8c2ab6d5580', 'U.VAN-T2', 'chore', 'Unity.SpaceGod', 372695, '2026-10-23T22:24:14.154Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a5fbddecf75926a7f', 'U.OVW-T2', 'chore', 'Unity.SpaceGod', 521870, '2026-10-23T22:30:50.885Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a6132a50ed606f736', 'UR-T68', 'chore', 'Unity.SpaceGod', 104864, '2026-10-24T01:45:54.382Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a63322263d5899abf', 'UH-T76', 'fix', 'Unity.SpaceGod', 41074, '2026-10-24T01:33:17.904Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a6608fda5fb341907', 'UNI-T47', 'feature', 'Unity.SpaceGod', 129797, '2026-10-23T23:10:14.991Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a669874a4bbd5bd10', 'UR-T21', 'fix', 'Unity.SpaceGod', 52433, '2026-10-23T22:14:33.89Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a6794ae5e59be049d', 'U.SPC-T506', 'fix', 'Unity.SpaceGod', 99553, '2026-10-23T19:33:10.955Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a73dc6b04ff7ef336', 'UNI-T34', 'feature', 'Unity.SpaceGod', 226260, '2026-10-24T00:33:49.099Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a7642f24f8e783023', 'UR-T66', 'fix', 'Unity.SpaceGod', 36759, '2026-10-24T01:12:07.694Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a7f48cd94c34055dd', 'UNI-T65', 'feature', 'Unity.SpaceGod', 136081, '2026-10-24T01:14:41.558Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a805ba8c87d56cc88', 'UNI-T28', 'fix', 'Unity.SpaceGod', 76779, '2026-10-23T22:19:45.612Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a81d3d2bd63b5b2d3', 'U.INC-T17', 'chore', 'Unity.SpaceGod', 93251, '2026-10-23T22:13:53.854Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a83125144bfed01c2', 'U.SPC-T463', 'fix', 'Unity.SpaceGod', 53707, '2026-10-23T17:03:20.534Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a84bac732f950c2de', 'UNI-T18', 'fix', 'Unity.SpaceGod', 122875, '2026-10-24T00:48:33.105Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a88964c80b55b290b', 'U.SPC-T480', 'feature', 'Unity.SpaceGod', 1401140, '2026-10-23T19:27:21.854Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a8b2d838d50026105', 'UR-T33', 'chore', 'Unity.SpaceGod', 66589, '2026-10-23T22:54:30.491Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a8cda9afc425dba1c', 'UR-T34', 'chore', 'Unity.SpaceGod', 785723, '2026-10-23T23:42:02.547Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a932de1e68c85be02', 'U.SPC-T487', 'feature', 'Unity.SpaceGod', 233466, '2026-10-23T17:46:29.609Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a95b1c165b303a628', 'U.VAN-T26', 'chore', 'Unity.SpaceGod', 25279, '2026-10-24T01:07:47.297Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a975bc07754e8707d', 'UNI-T31', 'chore', 'Unity.SpaceGod', 25203, '2026-10-23T22:50:27.203Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a98854c5c9aa3a227', 'UH-T16', 'feature', 'Unity.SpaceGod', 308999, '2026-10-23T23:40:20.902Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a98987f49231f1679', 'U.SPC-T470', 'feature', 'Unity.SpaceGod', 397042, '2026-10-23T18:19:04.256Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a98cbfd2385cf937e', 'UQD-T28', 'chore', 'Unity.SpaceGod', 191316, '2026-10-24T01:15:50.253Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a9ed7cb758584fcbe', 'U.VAN-T4', 'chore', 'Unity.SpaceGod', 99931, '2026-10-23T22:55:02.564Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-a9f14b7aa43007264', 'U.SPC-T477', 'feature', 'Unity.SpaceGod', 405673, '2026-10-23T20:30:54.714Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aa03a91542b8c0490', 'U.OVW-T39', 'chore', 'Unity.SpaceGod', 23980, '2026-10-24T01:08:04.331Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aa2102e70ded4260c', 'U.SPC-T482', 'feature', 'Unity.SpaceGod', 1771538, '2026-10-23T22:15:19.717Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aa4a740c839a35bd3', 'UR-T23', 'fix', 'Unity.SpaceGod', 169016, '2026-10-23T22:20:36.907Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aa67f3f878a03968a', 'U.INC-T21', 'chore', 'Unity.SpaceGod', 267688, '2026-10-24T01:41:59.476Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aaa08bddf83bcf456', 'UR-T24', 'fix', 'Unity.SpaceGod', 134520, '2026-10-23T22:21:34.565Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aad06e0437b19c3fd', 'UR-T35', 'chore', 'Unity.SpaceGod', 245789, '2026-10-24T01:13:22.271Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ab0712cd875a267fd', 'UR-T30', 'chore', 'Unity.SpaceGod', 174985, '2026-10-23T22:19:11.807Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aba37fbf6b27317b5', 'U.OVW-T6', 'chore', 'Unity.SpaceGod', 589853, '2026-10-24T01:40:49.308Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-abb82b10dc274c443', 'UH-T40', 'chore', 'Unity.SpaceGod', 1597978, '2026-10-24T01:57:35.931Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-abca465599ab88475', 'U.SPC-T479', 'feature', 'Unity.SpaceGod', 1145606, '2026-10-23T18:50:16.283Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-abcbebcc494886fe3', 'U.SPC-T488', 'feature', 'Unity.SpaceGod', 517128, '2026-10-23T18:29:51.6Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-abec163aa58aea231', 'U.SPC-T454', 'feature', 'Unity.SpaceGod', 215231, '2026-10-23T16:56:08.929Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ac2a21fd44187c82d', 'UQD-T26', 'chore', 'Unity.SpaceGod', 278567, '2026-10-23T23:17:00.521Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-acaa90de041dc2d87', 'UNI-T21', 'fix', 'Unity.SpaceGod', 90275, '2026-10-23T22:16:00.007Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-acd9dbb78ee736d20', 'UR-T19', 'fix', 'Unity.SpaceGod', 27285, '2026-10-23T22:12:02.544Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-acff9c0007a667ecb', 'UR-T60', 'fix', 'Unity.SpaceGod', 124338, '2026-10-24T00:12:18.541Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ad48553179d426ebc', 'U.SPC-T430', 'feature', 'Unity.SpaceGod', 1092537, '2026-10-23T17:49:04.175Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ada36ac529577d518', 'UNI-T25', 'chore', 'Unity.SpaceGod', 235637, '2026-10-23T22:21:30.063Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-adaa531778ea919c7', 'U.OVW-T5', 'chore', 'Unity.SpaceGod', 107474, '2026-10-23T22:55:56.954Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ade04df6e19ce9489', 'U.SPC-T426', 'feature', 'Unity.SpaceGod', 231953, '2026-10-23T17:01:58.218Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ae1d3f87194268b89', 'U.SPC-T484', 'feature', 'Unity.SpaceGod', 347595, '2026-10-23T18:11:19.794Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ae1f65c760d4f7571', 'UR-T63', 'fix', 'Unity.SpaceGod', 45675, '2026-10-24T01:14:48.504Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-ae3c81486112662db', 'U.SPC-T472', 'feature', 'Unity.SpaceGod', 856100, '2026-10-23T20:40:11.05Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-aef9446ad9c6dd118', 'UH-T34', 'chore', 'Unity.SpaceGod', 212607, '2026-10-23T22:21:36.389Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-af79b0e257edc4aad', 'U.SPC-T489', 'feature', 'Unity.SpaceGod', 1427036, '2026-10-23T18:44:46.744Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-af9e0bdd0283f7e65', 'U.SPC-T467', 'feature', 'Unity.SpaceGod', 179026, '2026-10-23T17:36:50.932Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ca9bd00-13a3-4fd3-b0f4-44f58e675c76/agent-afb2337b705cf8890', 'UH-T74', 'fix', 'Unity.SpaceGod', 204668, '2026-10-24T01:15:48.183Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-a475fe354cbc07f2b', 'M.ORCH-T473', 'fix', 'MindAttic.Orchestrator', 230004, '2026-10-19T19:52:56.097Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-a4e7c135868582316', 'M.ORCH-T471', 'feature', 'MindAttic.Orchestrator', 168953, '2026-10-19T18:02:41.672Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-a7cba2e3ec747c4d3', 'M.ORCH-T399', 'chore', 'MindAttic.Orchestrator', 58705, '2026-10-19T19:39:28.907Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-ab49d12287567f712', 'M.ORCH-T467', 'fix', 'MindAttic.Orchestrator', 101604, '2026-10-19T19:44:14.558Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-ab516d5ba2e550fdb', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 1282018, '2026-10-19T20:09:59.107Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1cf88640-dbf7-4b97-bb0a-a9cd74764d63/agent-ac8b6f92cdf0bdc40', 'M.ORCH-T470', 'fix', 'MindAttic.Orchestrator', 68404, '2026-10-19T19:42:06.789Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1d186b44-c738-4f96-a844-b9980d282b11', 'M.LNCH-T45', 'docs', 'MindAttic.Launcher', 37746, '2026-10-22T17:05:29.403Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1d244528-2ebd-45e8-9e03-7c2da10caee3', 'GG-T26', 'chore', 'GridGameGaiden', 84416, '2026-10-16T01:25:03.593Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1d29b23f-e12a-4476-ab0e-f1eaa52b5a55', 'M.ORCH-T779', 'chore', 'MindAttic.Orchestrator', 174043, '2026-10-24T05:49:44.278Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1d4dae76-55db-4041-b32a-7d0b3435b12f', 'GG-T38', 'feature', 'GridGameGaiden', 272031, '2026-10-15T13:47:12.042Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1e6b9429-db7b-4a76-b10f-f9b9f02aa23c', 'U.OVW-T5', 'chore', 'Unity.Overworld', 20322, '2026-10-23T22:55:26.477Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1e6ceacd-792e-461a-bfd8-88fc47637868', 'FOR-T18', 'docs', 'Formicarium', 15443, '2026-10-21T18:11:29.82Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a19d7f9d073754d46', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 4128000, '2026-10-16T21:52:49.488Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a3ceaa950aae0e1eb', 'U.SPC-T50', 'chore', 'UnitySolarSystem', 14988, '2026-10-16T15:11:18.367Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a6369f6642cdca902', 'U.SPC-T52', 'docs', 'UnitySolarSystem', 140912, '2026-10-16T15:43:37.089Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-a6f031fce3c085f38', 'U.SPC-T43', 'feature', 'UnitySolarSystem', 292184, '2026-10-16T16:08:44.817Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-ac700f62253add5d6', 'U.SPC-T54', 'feature', 'UnitySolarSystem', 462041, '2026-10-16T16:17:53.768Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-af6e752745078ff2e', 'U.SPC-T51', 'chore', 'UnitySolarSystem', 263122, '2026-10-16T16:02:14.355Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1efce509-103e-4ca5-9d0a-0a2b850ab93c/agent-af7fbfa4f3db5b308', 'U.SPC-T44', 'feature', 'UnitySolarSystem', 224994, '2026-10-16T15:56:44.605Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1fb53b01-f937-4979-ad11-192d797f2fb3', 'M.AUTH-T2', 'chore', 'MindAttic.Authentication', 125331, '2026-10-16T01:02:19.297Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1ff4f2d8-4c46-4b29-9f92-07ba59f9f33c', 'UH-T37', 'chore', 'Unity.Hare', 30092, '2026-10-24T00:05:09.153Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('203e7bea-da94-44c3-b7a0-da1f03358235', 'M.PSST-T2', 'chore', 'MindAttic.Psst', 45057, '2026-10-16T01:05:46.838Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a0328ed4cc14ae3e1', 'U.SPC-T371', 'feature', 'Unity.SpaceGod', 1395115, '2026-10-21T20:47:29.372Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a053f665e646f376b', 'U.SPC-T17', 'feature', 'Unity.SpaceGod', 188351, '2026-10-22T10:23:54.625Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a07288d17900f9b06', 'U.SPC-T246', 'chore', 'Unity.SpaceGod', 39051, '2026-10-22T01:12:06.914Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a1a9ea956600c5c86', 'U.SPC-T366', 'fix', 'Unity.SpaceGod', 244494, '2026-10-21T19:10:51.664Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a2e2425cda5fd8279', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 723776, '2026-10-22T06:09:28.256Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a37fcee84c3b1d58f', 'U.SPC-T247', 'chore', 'Unity.SpaceGod', 47951, '2026-10-22T01:16:54.314Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a47df1e19230b733e', 'U.SPC-T245', 'chore', 'Unity.SpaceGod', 34494, '2026-10-22T01:12:13.691Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a6fe9f5184ed02036', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 667441, '2026-10-22T09:21:57.147Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a7da8e7c42cf5a4d0', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 551955, '2026-10-22T02:22:38.409Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a946b1f6ff44e0bb7', 'U.SPC-T397', 'chore', 'Unity.SpaceGod', 88162, '2026-10-22T10:11:04.134Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-a9fe4bcd860a7afd3', 'U.SPC-T389', 'fix', 'Unity.SpaceGod', 28317, '2026-10-22T02:30:03.278Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-aabc585b99792c854', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 238931, '2026-10-22T01:10:54.479Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-aaf5f107c8f95db4f', 'U.SPC-T391', 'fix', 'Unity.SpaceGod', 37411, '2026-10-22T02:53:55.356Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-ab25accb0d233328a', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 2211398, '2026-10-22T11:39:21.609Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-ab3c1940cba18d963', 'U.SPC-T416', 'docs', 'Unity.SpaceGod', 29834, '2026-10-22T10:04:50.293Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-abc86f69aaa84a77b', 'U.SPC-T18', 'feature', 'Unity.SpaceGod', 344822, '2026-10-22T10:33:32.234Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-ac65990c81a610aa3', 'U.SPC-T378', 'chore', 'Unity.SpaceGod', 56730, '2026-10-22T01:18:08.186Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-ace8d7796737f9522', 'U.SPC-T380', 'fix', 'Unity.SpaceGod', 41731, '2026-10-22T02:20:50.648Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-adc67c2c1b334d99b', 'U.SPC-T283', 'chore', 'Unity.SpaceGod', 25918, '2026-10-22T01:12:41.412Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-aeb3973bdefc7e064', 'U.SPC-T390', 'fix', 'Unity.SpaceGod', 68075, '2026-10-22T02:39:34.659Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-aeb8b31f9d54cf958', 'U.SPC-T436', 'fix', 'Unity.SpaceGod', 160764, '2026-10-22T11:01:51.355Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2230fb99-d7f9-4acd-a1bb-06cfba1568bb/agent-af9b0da5084c367a4', 'U.SPC-T244', 'chore', 'Unity.SpaceGod', 68523, '2026-10-22T00:01:01.174Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('22ce4d90-c668-4388-aadf-585320eeff21', 'U.INC-T19', 'chore', 'Unity.Incognito', 29244, '2026-10-24T01:10:05.936Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2305b05e-e329-4d82-92a1-75d3fd03d38b', 'U.SPC-T397', 'chore', 'Unity.SpaceGod', 23097, '2026-10-22T10:08:49.274Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('23c96b9d-6918-4855-81df-44992e462d4c', 'SS-T25', 'chore', 'Prose', 19465, '2026-10-15T11:56:34.07Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('24c789dc-69cb-49bf-974c-1ec48da0b50d', 'GG-T37', 'feature', 'GridGameGaiden', 89563, '2026-10-15T13:37:25.28Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('25e72d46-fc7c-4e32-937d-19b9d0f97fb0', 'UNI-T22', 'fix', 'Unity.Chain', 46848, '2026-10-23T22:15:31.51Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('27fe7ce7-120f-4946-9130-122a3879b678', 'U.SPC-T41', 'feature', 'UnitySolarSystem', 159769, '2026-10-15T11:19:43.96Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('290a401f-e0f6-4678-aac3-361ea19a5492', 'GG-T45', 'feature', 'GridGameGaiden', 326932, '2026-10-15T15:32:45.457Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a07085f-b3c3-4394-b658-44fd1d331a05', 'M.VALT-T2', 'chore', 'MindAttic.Vault', 44565, '2026-10-16T01:07:57.963Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a3a98c09180fb6719', 'RB-T22', 'feature', 'REA.Bloodlines', 432528, '2026-10-22T06:30:53.73Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a4267bbb4089ba056', 'RB-T21', 'feature', 'REA.Bloodlines', 378780, '2026-10-22T06:42:10.08Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a4841c2f881e16bf9', 'RB-T26', 'feature', 'REA.Bloodlines', 307759, '2026-10-22T11:56:14.402Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a64c3a79cb5fc9ddd', 'RB-T24', 'feature', 'REA.Bloodlines', 533003, '2026-10-22T10:50:09.354Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a676a077b2832c473', 'RB-T23', 'feature', 'REA.Bloodlines', 798978, '2026-10-22T07:59:22.681Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a693cc6a2fd2f4316', 'RB-T20', 'feature', 'REA.Bloodlines', 491127, '2026-10-22T04:01:42.271Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-a77a92e34333b43c7', 'RB-T25', 'feature', 'REA.Bloodlines', 375615, '2026-10-22T11:52:36.377Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-ac462b61b0dcbf5b3', 'RB-T29', 'fix', 'REA.Bloodlines', 59688, '2026-10-22T04:55:38.949Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2a2ff560-06bf-4989-8ce4-23189afbaada/agent-ad51c31dc41b6a00d', 'RB-T27', 'fix', 'REA.Bloodlines', 290438, '2026-10-22T02:35:46.13Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a04547694dc6b9ed1', 'GG-T17', 'chore', 'Unity.GridGame', 371835, '2026-10-21T08:02:11.957Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a120c4bcbc52a0494', 'GG-T48', 'feature', 'Unity.GridGame', 865021, '2026-10-21T08:42:29.611Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a478ba647e4123112', 'GG-T56', 'feature', 'Unity.GridGame', 337829, '2026-10-21T13:23:20.326Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a52a7b6e5d56fb673', 'GG-T159', 'fix', 'Unity.GridGame', 247849, '2026-10-21T08:55:39.845Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a5f7ac405e826f34e', 'GG-T157', 'docs', 'Unity.GridGame', 112770, '2026-10-21T06:48:25.448Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a64ca1b45db93e8fd', 'GG-T18', 'feature', 'Unity.GridGame', 76093, '2026-10-21T07:41:49.793Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a8a7915f15f9300ef', 'GG-T53', 'feature', 'Unity.GridGame', 397162, '2026-10-21T13:30:45.571Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a8a888c970b8719c3', 'GG-T137', 'docs', 'Unity.GridGame', 381296, '2026-10-21T05:43:23.004Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-a8c98a87563f506a8', 'GG-T54', 'feature', 'Unity.GridGame', 447799, '2026-10-21T13:26:00.312Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-ad4829ea069ef148d', 'GG-T125', 'chore', 'Unity.GridGame', 103058, '2026-10-21T07:13:50.97Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-ad5e46e55e9110bdb', 'GG-T52', 'feature', 'Unity.GridGame', 367016, '2026-10-21T13:30:57.896Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b46e72b-03e8-439f-8047-9ef30321cc23/agent-ae8576c567e4dc8de', 'GG-T65', 'chore', 'Unity.GridGame', 168702, '2026-10-21T06:55:08.306Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2b906ad4-d851-4bc5-9655-1a1d3ebf3775', 'RB-T160', 'fix', 'REA.Bloodlines', 22506, '2026-10-24T00:21:05.292Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2bb4867e-31ed-479b-9b9a-426c662b86e6', 'GG-T30', 'chore', 'GridGameGaiden', 139930, '2026-10-15T11:19:44.723Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2c51efb7-7fba-4b21-8c6f-9cc9c2ac59a2', 'GG-T46', 'feature', 'GridGameGaiden', 385490, '2026-10-15T15:20:35.827Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2cb16b93-720e-400b-817b-a0ec575a507c', 'M.ORCH-T631', 'chore', 'MindAttic.Orchestrator', 25007, '2026-10-23T01:02:00.298Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2cd2e503-c388-43a5-a723-89d9354d9aee', 'USS-T44', 'feature', 'UnitySolarSystem', 255951, '2026-10-15T11:19:51.119Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2e908411-5591-4107-9c97-c17511fa302b', 'MAT-T53', 'docs', 'MindAttic.Agency', 46134, '2026-10-24T06:44:29.524Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2efe68bb-8df4-4797-a605-fc49205bbc52', 'GG-T39', 'feature', 'GridGameGaiden', 437324, '2026-10-15T14:22:03.227Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2f1aced6-85ec-4a7f-ac09-7168b62b2aa5', 'U.SPC-T33', 'chore', 'UnitySolarSystem', 184785, '2026-10-15T10:55:30.855Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2f7dc099-518a-40fb-a305-1c69ff7107c3', 'U.VAN-T4', 'chore', 'Unity.ProjectVania', 13596, '2026-10-23T22:54:21.824Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2f869b18-505e-4dd4-baa4-dd182fc97114', 'AUT-T7', 'chore', 'Automata', 124342, '2026-10-16T00:28:11.312Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('30a9bdc9-e00e-42c1-9728-ab2496765886', 'SS-T84', 'chore', 'Prose', 884833, '2026-10-15T20:34:37.505Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('320acbe9-0043-4941-be4d-2741433f8e7d', 'GG-T41', 'feature', 'GridGameGaiden', 326932, '2026-10-15T15:05:18.842Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('32249a3f-9e1c-4e2e-9d49-d0e0d30e38c2', 'M.ORCH-T647', 'fix', 'MindAttic.Orchestrator', 17810, '2026-10-23T01:05:13.182Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('326b33a7-da2b-426a-9e1a-6eb790cacacf', 'SS-T90', 'chore', 'Prose', 194445, '2026-10-15T18:50:12.639Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a3ae72f7274821c2e', 'MAT-T49', 'fix', '(workspace root)', 174581, '2026-10-24T06:59:56.114Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a3ceac6277f4c93ad', 'MAT-T16', 'fix', '(workspace root)', 280434, '2026-10-24T04:58:37.526Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a3e94db8ffc798db8', 'MAT-T9', 'docs', '(workspace root)', 80251, '2026-10-24T04:42:10.573Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a4f3fc678db3b7bdf', 'MAT-T26', 'chore', '(workspace root)', 1588432, '2026-10-24T06:38:17.308Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a5f1a6bd2244e210b', 'MAT-T39', 'chore', '(workspace root)', 58077, '2026-10-24T05:13:44.626Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a6937c2f3c85e52af', 'MAT-T53', 'docs', '(workspace root)', 141972, '2026-10-24T06:48:34.07Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a6e803ab6d690a147', 'MAT-T50', 'fix', '(workspace root)', 262986, '2026-10-24T06:53:04.347Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a9309333aa62c3b4b', 'MAT-T46', 'fix', '(workspace root)', 128153, '2026-10-24T05:14:55.258Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-a93217be88cc486ca', 'MAT-T52', 'chore', '(workspace root)', 51732, '2026-10-24T06:41:13.057Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-ab05782272c9e7819', 'MAT-T51', 'fix', '(workspace root)', 342260, '2026-10-24T07:02:28.313Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-ac67fa2039dee617c', 'MAT-T25', 'chore', '(workspace root)', 279861, '2026-10-24T05:06:14.306Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-adb14723af5063736', 'MAT-T34', 'fix', '(workspace root)', 53459, '2026-10-24T05:11:09.874Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3355791e-fb1c-436b-9d8e-7fb6aeda09fd/agent-af468083962863453', 'MAT-T40', 'feature', '(workspace root)', 102819, '2026-10-24T05:20:47.722Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a0e0fa42a56e7b4df', 'UNI-T56', 'feature', 'Unity.Chain', 356294, '2026-10-24T16:55:42.2Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a0fc7757fd38ec7ce', 'UNI-T40', 'feature', 'Unity.Chain', 255418, '2026-10-24T18:01:50.898Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a12ff691c05ffe0d5', 'UNI-T19', 'fix', 'Unity.Chain', 477240, '2026-10-24T17:15:23.674Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a4ed6a09255a98f45', 'UNI-T20', 'fix', 'Unity.Chain', 342908, '2026-10-24T17:49:42.75Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-aa8637a8445a34daf', 'UNI-T58', 'feature', 'Unity.Chain', 150370, '2026-10-24T17:44:04.114Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-abed28cbfcbef017d', 'UNI-T70', 'fix', 'Unity.Chain', 114705, '2026-10-24T17:40:48.395Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-adc498df063fa5040', 'UNI-T57', 'feature', 'Unity.Chain', 191711, '2026-10-24T17:21:39.677Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3454ada6-20f3-472b-ae50-1508c1c7b952', 'U.SPC-T436', 'fix', 'Unity.SpaceGod', 31355, '2026-10-22T10:52:29.657Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3465b5a1-50cc-4ba4-9d42-5520d2a103f1', 'SS-T66', 'chore', 'Prose', 70344, '2026-10-15T16:47:35.709Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('34f48634-9016-42f5-9d89-f55f1e1d9548', 'U.SPC-T35', 'chore', 'UnitySolarSystem', 290819, '2026-10-15T11:06:09.699Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('34f7fc98-c887-49de-a7fe-a56dceb2ac23', 'MAI-T3', 'chore', 'MindAttic.Ideas', 162693, '2026-10-16T01:51:34.055Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35741c8f-f8ad-43d1-83c0-3b750e46d397', 'M.LNCH-T21', 'fix', 'MindAttic.Launcher', 109561, '2026-10-16T00:32:53.119Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-a17a460a481b9a6ed', 'U.INC-T26', 'chore', 'Unity.Incognito', 627496, '2026-10-24T14:27:22.827Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-a2e4cea63fb6636a6', 'U.INC-T22', 'chore', 'Unity.Incognito', 576601, '2026-10-24T12:09:47.69Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-a32077db575cc9ce1', 'U.INC-T24', 'chore', 'Unity.Incognito', 354137, '2026-10-24T12:58:17.985Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-a45e5b055e31529e2', 'U.INC-T25', 'chore', 'Unity.Incognito', 360451, '2026-10-24T13:32:50.744Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-a6df2ec719e00a8cc', 'U.INC-T28', 'feature', 'Unity.Incognito', 181214, '2026-10-24T15:22:13.309Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-a79d53fe037967879', 'U.INC-T27', 'chore', 'Unity.Incognito', 252287, '2026-10-24T15:05:06.04Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('35906ceb-2095-4476-bc1b-8d0a0028b287/agent-abd425993d3fb8cba', 'U.INC-T23', 'chore', 'Unity.Incognito', 306348, '2026-10-24T12:29:02.015Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a238312c4e5ffc775', 'U.SPC-T317', 'fix', 'UnitySpace', 195104, '2026-10-20T03:52:21.946Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a3720cd08da9d5ccd', 'U.SPC-T320', 'fix', 'UnitySpace', 114009, '2026-10-20T03:08:04.864Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a44a2859b46863bbd', 'U.SPC-T319', 'fix', 'UnitySpace', 149143, '2026-10-20T03:52:49.629Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a50d9d7aa1a8ac467', 'U.SPC-T321', 'fix', 'UnitySpace', 210848, '2026-10-20T03:52:19.223Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a73d86ed921766dfa', 'U.SPC-T49', 'chore', 'UnitySpace', 266767, '2026-10-20T03:53:01.276Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a752314e74a417631', 'U.SPC-T322', 'fix', 'UnitySpace', 137666, '2026-10-20T03:52:59.822Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-a7817391489e0aa19', 'U.SPC-T318', 'fix', 'UnitySpace', 177550, '2026-10-20T03:52:26.149Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37899c41-b36d-402f-a64d-8db182db7e91/agent-aed248f922b41911c', 'U.SPC-T316', 'fix', 'UnitySpace', 216662, '2026-10-20T03:50:32.947Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37981662-0d80-4a46-8b6c-0fa9ac3908a4', 'AUT-T10', 'chore', 'Automata', 71430, '2026-10-16T00:55:42.197Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37d38e1f-baab-4d7a-b1d8-e48fd9c9ca23', 'LOG-T3', 'chore', 'MindAttic.Log', 184183, '2026-10-16T02:01:31.335Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3804a5cf-3d40-4bb1-8197-1397553b6030', 'AUT-T17', 'docs', 'Automata', 16698, '2026-10-21T18:11:21.751Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a2ba785-a6d2-4d7c-8caa-befa2e1e1818', 'SS-T63', 'chore', 'Prose', 71935, '2026-10-15T17:14:47.601Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-a07e21a1fe0771586', 'CLA-T4', 'chore', '(workspace root)', 132779, '2026-10-21T06:47:06.456Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-a2c10f4bfbc1aa032', 'JF-T4', 'chore', '(workspace root)', 131202, '2026-10-21T06:46:22.985Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-a627afe69f386b329', 'CM-T4', 'chore', '(workspace root)', 182373, '2026-10-21T06:54:40.887Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-a6a2e6aae58671654', 'M.CRYP-T4', 'chore', '(workspace root)', 21197, '2026-10-21T06:31:51.663Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-a72c0dd11862094d5', 'JFP-T3', 'chore', '(workspace root)', 59435, '2026-10-21T04:47:23.308Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-af6847464e4777adb', 'G.EZ-T94', 'docs', '(workspace root)', 24599, '2026-10-21T04:37:33.897Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-af7c67a64f8be0090', 'M.ORCH-T564', 'chore', '(workspace root)', 59225, '2026-10-21T04:37:18.706Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3a56e2a7-bd2a-4e61-83f9-39728b569c1d/agent-afe98d6d6a7d6fb5e', 'M.EXPT-T3', 'chore', '(workspace root)', 287954, '2026-10-21T07:00:45.94Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3acf6e14-b79d-4350-8575-9184c3198607', 'RB-T144', 'fix', 'REA.Bloodlines', 24234, '2026-10-23T14:04:35.169Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3cce25bb-e193-49dc-a217-1992b4a8be10', 'JFP-T6', 'docs', 'JellyFinPoster', 15373, '2026-10-21T18:10:14.843Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d206b98-1dc7-4d8b-be76-36ee9806c41d/agent-a82ff2123ff9b16b5', 'PL-T15', 'docs', 'ProcessLogger', 216510, '2026-10-20T00:21:51.418Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d206b98-1dc7-4d8b-be76-36ee9806c41d/agent-aab7ed261f4aa74ca', 'PL-T12', 'chore', 'ProcessLogger', 267096, '2026-10-20T00:54:14.631Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d5febb0-0770-4468-8970-23920ce5de7e/agent-a6295bbad76b8e7bf', 'UC-T17', 'chore', 'Unity.Cursory', 161038, '2026-10-21T06:38:57.765Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d5febb0-0770-4468-8970-23920ce5de7e/agent-a94a360e79ea8f89d', 'UC-T7', 'feature', 'Unity.Cursory', 521711, '2026-10-21T13:30:51.802Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d7df0e1-426c-4860-af99-2f9aace9d486', 'M.LEGN-T20', 'docs', 'MindAttic.Legion', 19353, '2026-10-21T18:11:40.915Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a0d8ce001efc0c9db', 'M.ORCH-T128', 'chore', 'MindAttic.Orchestrator', 49795, '2026-10-16T15:18:48.278Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a4cea79899ba7a725', 'M.ORCH-T127', 'chore', 'MindAttic.Orchestrator', 51166, '2026-10-16T15:14:42.971Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a6d913c62e00362e7', 'M.ORCH-T126', 'chore', 'MindAttic.Orchestrator', 66558, '2026-10-16T15:23:33.133Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-a751a1fc3487a6bc7', 'M.ORCH-T132', 'chore', 'MindAttic.Orchestrator', 44033, '2026-10-16T15:25:12.214Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-ace5de353eb4fd0a1', 'M.ORCH-T129', 'chore', 'MindAttic.Orchestrator', 33059, '2026-10-16T15:10:43.781Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-ad2e0a4db0fd59194', 'M.ORCH-T130', 'chore', 'MindAttic.Orchestrator', 70219, '2026-10-16T15:35:26.257Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3d947770-b98f-49e2-a296-a53da372e779/agent-aed374a90ad1c162e', 'M.ORCH-T131', 'chore', 'MindAttic.Orchestrator', 87154, '2026-10-16T15:43:39.032Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-a45e552c0f832690d', 'M.FLOW-T62', 'docs', 'MindAttic.Workflow', 136403, '2026-10-19T02:31:17.525Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-a46f0190026730abb', 'M.FLOW-T56', 'fix', 'MindAttic.Workflow', 122651, '2026-10-19T02:17:58.772Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-a4f227a852a735f8d', 'M.FLOW-T59', 'chore', 'MindAttic.Workflow', 857161, '2026-10-19T04:44:20.904Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-aa16a79874bed75f0', 'M.FLOW-T95', 'fix', 'MindAttic.Workflow', 72918, '2026-10-19T02:36:38.224Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-ad31ea860c2c791bf', 'M.FLOW-T58', 'fix', 'MindAttic.Workflow', 243773, '2026-10-19T02:27:05.998Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e576952-425c-465f-87a0-d9341e4f8bf8/agent-afddfa587628e67ba', 'M.FLOW-T51', 'fix', 'MindAttic.Workflow', 164817, '2026-10-19T02:20:26.023Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a482a483a98abf5e4', 'RB-T99', 'fix', 'REA.Bloodlines', 73244, '2026-10-23T11:28:18.73Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a55c6a88c5b4636d9', 'RB-T123', 'fix', 'REA.Bloodlines', 179740, '2026-10-23T12:33:52.044Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a6082f6f40239b62a', 'RB-T100', 'fix', 'REA.Bloodlines', 66649, '2026-10-23T05:33:08.44Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a7ae59a48b2a6686d', 'RB-T101', 'fix', 'REA.Bloodlines', 38972, '2026-10-23T08:24:19.028Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a8b3751fc6292bc45', 'RB-T124', 'fix', 'REA.Bloodlines', 54309, '2026-10-23T12:55:02.454Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a919b57ce318cf14b', 'RB-T102', 'fix', 'REA.Bloodlines', 76763, '2026-10-23T10:26:05.003Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-a97874de7ad7f1645', 'RB-T103', 'fix', 'REA.Bloodlines', 83384, '2026-10-23T11:22:05.922Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-ab9931bbf039efa11', 'RB-T99', 'fix', 'REA.Bloodlines', 76344, '2026-10-23T05:32:05Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3e58b60f-79d6-4808-ac72-688965c03eb8/agent-afeed9b60a8e7cf67', 'RB-T95', 'fix', 'REA.Bloodlines', 39781, '2026-10-23T05:32:38.929Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3f18482e-ae5a-4b91-8dad-20355d6415c6', 'M.ORCH-T765', 'fix', 'MindAttic.Orchestrator', 11751, '2026-10-24T01:37:05.954Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3f9e21d6-7980-4f2f-979e-dcd6cc46c3a7', 'U.VAN-T5', 'chore', 'Unity.ProjectVania', 86466, '2026-10-24T01:17:17.507Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('401c6587-2762-44a7-8561-08643f6925f0', 'SS-T65', 'chore', 'Prose', 175333, '2026-10-15T17:26:04.955Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a03780c4996b4cff5', 'U.INC-T11', 'feature', '(workspace root)', 345948, '2026-10-23T00:19:32.803Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a0ca86d2956c876e9', 'U.INC-T13', 'feature', '(workspace root)', 229943, '2026-10-23T00:36:53.199Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a1367b435fccecfd1', 'UR-T3', 'feature', '(workspace root)', 288421, '2026-10-23T00:44:36.45Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a26a4a1fb7d1bf984', 'U.INC-T16', 'chore', '(workspace root)', 22816, '2026-10-23T00:47:32.047Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a3357460c0dc74697', 'UR-T4', 'feature', '(workspace root)', 317106, '2026-10-23T01:34:00.424Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a6d85f2ac31fa2a9d', 'U.INC-T15', 'fix', '(workspace root)', 57680, '2026-10-23T01:01:54.008Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a7d88274702610b40', 'U.INC-T7', 'feature', '(workspace root)', 215173, '2026-10-23T00:10:07.213Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-a7fe5c54d573b2df7', 'U.INC-T14', 'fix', '(workspace root)', 63709, '2026-10-23T00:50:35.282Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-aa2f300eaf97b0b92', 'UNI-T4', 'feature', '(workspace root)', 108540, '2026-10-23T01:26:41.922Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-aaeaeb745dca1c02b', 'UR-T5', 'feature', '(workspace root)', 406853, '2026-10-23T02:03:42.688Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-addb9b259136ba07b', 'UNI-T5', 'feature', '(workspace root)', 80572, '2026-10-23T01:24:45.318Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-af3b748d603ecc138', 'UH-T7', 'feature', '(workspace root)', 109481, '2026-10-22T23:52:58.024Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('41df9bb7-c213-4b78-b6a6-4eec9591facb/agent-af78a7e9b9809415e', 'UH-T6', 'feature', '(workspace root)', 123942, '2026-10-22T23:54:18.06Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('44c614df-9ad6-4700-80e8-041f58238120', 'SS-T87', 'chore', 'Prose', 676481, '2026-10-15T20:48:53.883Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('450b31e8-a4ee-4975-bbf9-b4d545f393a3', 'UNI-T20', 'fix', 'Unity.Chain', 59298, '2026-10-24T17:44:44.719Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4735b563-69e7-438d-b563-834a57645702', 'U.VAN-T6', 'chore', 'Unity.ProjectVania', 63779, '2026-10-24T10:32:23.106Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-a385401694d4da8a8', 'U.INC-T9', 'feature', 'Unity.Incognito', 281094, '2026-10-22T10:42:44.317Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-a5450f7e05ef6431b', 'U.INC-T10', 'feature', 'Unity.Incognito', 148870, '2026-10-22T10:26:12.033Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-a8c1dd18e823cf472', 'U.INC-T4', 'feature', 'Unity.Incognito', 96625, '2026-10-22T08:55:47.743Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-abf750e7431c9f8b7', 'U.INC-T8', 'feature', 'Unity.Incognito', 220455, '2026-10-22T10:33:11.04Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-abfdc61f3aafddb47', 'U.INC-T6', 'feature', 'Unity.Incognito', 219224, '2026-10-22T11:05:07.233Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-ad8684c5ce5db1c60', 'U.INC-T5', 'feature', 'Unity.Incognito', 275888, '2026-10-22T10:17:10.355Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a1126f86931dcfa8d', 'RB-T145', 'fix', 'REA.Bloodlines', 68055, '2026-10-23T14:16:21.085Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a1d691ff110cab8e1', 'RB-T147', 'fix', 'REA.Bloodlines', 87571, '2026-10-23T14:21:39.704Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a30a5bfd6951989be', 'RB-T149', 'fix', 'REA.Bloodlines', 57342, '2026-10-23T14:17:41.534Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a4f833c29c8b98c5e', 'RB-T146', 'fix', 'REA.Bloodlines', 44932, '2026-10-23T14:17:23.051Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a5fbe52a0daa1cec5', 'RB-T144', 'fix', 'REA.Bloodlines', 43947, '2026-10-23T14:06:35.141Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a779bee3e62b7aa83', 'RB-T125', 'fix', 'REA.Bloodlines', 39114, '2026-10-23T13:56:12.345Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-a7f22fd6eb67c6464', 'RB-T148', 'fix', 'REA.Bloodlines', 38469, '2026-10-23T14:20:08.704Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-aa23b6ce1fb1b5f6b', 'RB-T133', 'fix', 'REA.Bloodlines', 29616, '2026-10-23T13:57:31.59Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-abe6f07cfcd6a12da', 'RB-T126', 'fix', 'REA.Bloodlines', 55846, '2026-10-23T13:56:25.927Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-ae0706ecd5be7d6ed', 'RB-T131', 'fix', 'REA.Bloodlines', 28816, '2026-10-23T13:56:53.573Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-aee097a6c8e4f7ba2', 'RB-T137', 'fix', 'REA.Bloodlines', 83523, '2026-10-23T14:02:06.212Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('48fbfb83-d752-4589-a0fd-bac2de388ae2/agent-afc1e48e52e61c388', 'RB-T143', 'fix', 'REA.Bloodlines', 18984, '2026-10-23T14:02:44.571Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4c3b85b1-1314-4760-8c2d-dc7646297287', 'GG-T18', 'feature', 'GridGameGaiden', 51661, '2026-10-15T09:03:05.17Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4e31197a-bbdf-45da-9680-649daa08074b', 'GG-T36', 'feature', 'GridGameGaiden', 493758, '2026-10-15T14:55:05.913Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a0b9cc9b462364bb0', 'UNI-T17', 'fix', 'Unity.Chain', 84516, '2026-10-23T10:34:48.588Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a0d7923d1756176d7', 'UNI-T8', 'feature', 'Unity.Chain', 118087, '2026-10-23T06:50:58.547Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a132278f9cb93e894', 'UNI-T16', 'fix', 'Unity.Chain', 285891, '2026-10-23T10:55:15.166Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a693b8e87902bd91f', 'UNI-T7', 'feature', 'Unity.Chain', 70828, '2026-10-23T05:38:16.835Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a7d9fd7f9ee0c5d1a', 'UNI-T9', 'feature', 'Unity.Chain', 129302, '2026-10-23T07:20:20.584Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-aa3cde8df6b02423a', 'UNI-T10', 'feature', 'Unity.Chain', 288764, '2026-10-23T08:58:53.686Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-abb43334c33d29779', 'UNI-T6', 'feature', 'Unity.Chain', 114885, '2026-10-23T05:45:21.135Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-ad474e7221b75f45f', 'UNI-T12', 'feature', 'Unity.Chain', 164853, '2026-10-23T09:38:28.172Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('50b04d70-5d44-456c-91cd-ef5fe59673d4', 'GG-T40', 'feature', 'GridGameGaiden', 199068, '2026-10-15T14:08:41.696Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('51870e7d-1d97-4418-8b48-6097fb43ff7e', 'M.LNCH-T16', 'chore', 'MindAttic.Launcher', 25041, '2026-10-23T19:17:09.226Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52728cb7-3bd9-4584-bd3c-85b160ecb8fc', 'MB-T2', 'chore', 'MediaButler', 89617, '2026-10-16T00:57:42.857Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0ba12577fa8e45dc', 'M.COMP-T38', 'feature', 'MindAttic.Orchestrator', 127618, '2026-10-20T16:39:34.278Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0d42cfb2a06475a7', 'M.ORCH-T529', 'feature', 'MindAttic.Orchestrator', 435180, '2026-10-20T16:29:43.21Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0e3d919b3a5943fa', 'M.COMP-T40', 'fix', 'MindAttic.Orchestrator', 61821, '2026-10-20T16:35:54.797Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a0f84cbc3c47f3062', 'M.ORCH-T498', 'feature', 'MindAttic.Orchestrator', 303584, '2026-10-20T16:37:37.972Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a19f81cedda9a64df', 'M.ORCH-T531', 'feature', 'MindAttic.Orchestrator', 479681, '2026-10-20T17:19:26.346Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a20a26aed61bbdf9e', 'M.ORCH-T502', 'fix', 'MindAttic.Orchestrator', 158766, '2026-10-20T10:42:47.461Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a26193165d7117e73', 'M.COMP-T37', 'feature', 'MindAttic.Orchestrator', 140102, '2026-10-20T16:40:17.405Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2b76bb8af5b62e8f', 'M.COMP-T4', 'feature', 'MindAttic.Orchestrator', 118161, '2026-10-20T17:36:06.36Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2b90151de4c3863e', 'M.ORCH-T528', 'feature', 'MindAttic.Orchestrator', 2090587, '2026-10-20T17:23:13.756Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2cd508c9e0427428', 'M.ORCH-T493', 'feature', 'MindAttic.Orchestrator', 89172, '2026-10-20T13:54:41.762Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a2d518f9fe6ff25de', 'M.ORCH-T523', 'fix', 'MindAttic.Orchestrator', 296292, '2026-10-20T13:06:30.802Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a39b229ffbba9a8e5', 'M.ORCH-T524', 'feature', 'MindAttic.Orchestrator', 789753, '2026-10-20T14:36:09.751Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a3fb971ffe8f2f47e', 'M.COMP-T12', 'feature', 'MindAttic.Orchestrator', 151487, '2026-10-20T16:23:33.071Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a430fc71ce4411b0f', 'M.ORCH-T498', 'feature', 'MindAttic.Orchestrator', 30351, '2026-10-20T04:24:09.084Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a43a48eb87b11f082', 'M.ORCH-T525', 'fix', 'MindAttic.Orchestrator', 74217, '2026-10-20T13:34:21.633Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a457b5c1ffa0a2f88', 'M.COMP-T34', 'feature', 'MindAttic.Orchestrator', 106285, '2026-10-20T16:37:41.581Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a464d6b0ca2b01a25', 'M.ORCH-T496', 'fix', 'MindAttic.Orchestrator', 73702, '2026-10-20T04:30:17.589Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a5ab31c6cb3776a50', 'M.COMP-T10', 'docs', 'MindAttic.Orchestrator', 64348, '2026-10-20T15:57:30.01Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a5e3bf02493eb12e0', 'M.ORCH-T172', 'chore', 'MindAttic.Orchestrator', 2087498, '2026-10-20T11:59:22.884Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a64f9bfabb9ab6a6f', 'M.ORCH-T500', 'feature', 'MindAttic.Orchestrator', 295353, '2026-10-20T04:40:01.934Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a660804f3b7fd3d50', 'M.ORCH-T492', 'feature', 'MindAttic.Orchestrator', 388036, '2026-10-20T13:41:11.251Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a6935387cda2ec8ca', 'M.ORCH-T494', 'chore', 'MindAttic.Orchestrator', 954085, '2026-10-20T14:07:08.604Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a6af75f799b6888a9', 'M.COMP-T36', 'fix', 'MindAttic.Orchestrator', 40873, '2026-10-20T16:18:19.969Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a6d98bb62bf797436', 'M.ORCH-T499', 'feature', 'MindAttic.Orchestrator', 248751, '2026-10-20T16:16:29.137Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a75a0d96a7e02eb09', 'M.ORCH-T182', 'chore', 'MindAttic.Orchestrator', 106608, '2026-10-20T17:41:14.182Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a75e220ba5346cd10', 'M.COMP-T30', 'feature', 'MindAttic.Orchestrator', 163133, '2026-10-20T16:24:21.238Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a7b97de0e8dfea5b8', 'M.COMP-T32', 'feature', 'MindAttic.Orchestrator', 80707, '2026-10-20T16:20:25.464Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a7cfe17aeceaf179b', 'M.ORCH-T501', 'feature', 'MindAttic.Orchestrator', 300838, '2026-10-20T04:54:48.255Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a8282d634083be933', 'M.ORCH-T558', 'feature', 'MindAttic.Orchestrator', 254273, '2026-10-21T00:12:36.677Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a88e1db7d3fce96f8', 'M.COMP-T7', 'chore', 'MindAttic.Orchestrator', 357008, '2026-10-20T16:14:10.969Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a8af468aaa5235af7', 'M.COMP-T33', 'feature', 'MindAttic.Orchestrator', 75288, '2026-10-20T17:02:41.02Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a908577035ef64e91', 'M.ORCH-T181', 'chore', 'MindAttic.Orchestrator', 405457, '2026-10-20T18:02:59.698Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a98dc4b914b0e5ff3', 'M.ORCH-T497', 'fix', 'MindAttic.Orchestrator', 140629, '2026-10-20T04:37:33.183Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a99ec9a1cc49b31a6', 'M.COMP-T29', 'fix', 'MindAttic.Orchestrator', 34527, '2026-10-20T16:14:36.664Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-a9a5a2476da7e1eb1', 'M.ORCH-T522', 'feature', 'MindAttic.Orchestrator', 208385, '2026-10-20T13:14:37.541Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa05a0b7041b1d2bb', 'M.COMP-T39', 'fix', 'MindAttic.Orchestrator', 74002, '2026-10-20T16:35:20.466Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa582eb0a887d80c3', 'M.COMP-T31', 'feature', 'MindAttic.Orchestrator', 108326, '2026-10-20T16:22:45.147Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa86d28e439ee3cbb', 'M.ORCH-T495', 'fix', 'MindAttic.Orchestrator', 389981, '2026-10-20T04:43:59.15Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aa8937aa055a4ad82', 'M.COMP-T11', 'chore', 'MindAttic.Orchestrator', 157347, '2026-10-20T17:38:22.036Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-ab2843c7ad2d8dbc1', 'M.ORCH-T527', 'feature', 'MindAttic.Orchestrator', 241614, '2026-10-20T15:27:23.383Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-abb2d704e194dfa18', 'M.COMP-T28', 'fix', 'MindAttic.Orchestrator', 42183, '2026-10-20T16:15:24.821Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-abdd3222004aae1f1', 'M.COMP-T8', 'chore', 'MindAttic.Orchestrator', 35524, '2026-10-20T15:57:12.759Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-abeaee46dbf709d0b', 'M.COMP-T9', 'chore', 'MindAttic.Orchestrator', 176064, '2026-10-20T16:12:36.109Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-adcb6fabe9f6665b6', 'M.COMP-T35', 'feature', 'MindAttic.Orchestrator', 73156, '2026-10-20T16:36:58.098Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-adf19216e2400429c', 'M.ORCH-T532', 'fix', 'MindAttic.Orchestrator', 369744, '2026-10-20T16:59:45.332Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-ae5aee90badb46820', 'M.ORCH-T526', 'feature', 'MindAttic.Orchestrator', 908034, '2026-10-20T15:11:20.618Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aee4e29ec4e1b2f0a', 'M.ORCH-T490', 'feature', 'MindAttic.Orchestrator', 397963, '2026-10-20T13:21:37.056Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-aefd185e8a3bb8c7e', 'M.COMP-T15', 'feature', 'MindAttic.Orchestrator', 134633, '2026-10-20T15:55:02.57Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5300d88e-be3b-441a-976c-34fc3e25e676/agent-af40bf46a49b0dda9', 'M.ORCH-T530', 'fix', 'MindAttic.Orchestrator', 175125, '2026-10-20T16:32:11.755Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('53cd35c6-a8a8-4506-bc87-ded828a62de3', 'U.INC-T24', 'chore', 'Unity.Incognito', 248311, '2026-10-24T12:53:38.578Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('55499699-7a90-4387-8d6f-b77d1952862b', 'RB-T45', 'fix', 'REA.Bloodlines', 6896, '2026-10-22T16:54:07.112Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('555266d3-de95-4a86-beb2-152b31adedd1/agent-a95841e48c5a3d5ab', 'UM-T71', 'feature', 'Unity.Multiplayer', 902513, '2026-10-23T06:45:07.701Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('555266d3-de95-4a86-beb2-152b31adedd1/agent-ae8a12e2645cf66b7', 'UM-T72', 'fix', 'Unity.Multiplayer', 323390, '2026-10-23T06:18:27.842Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('55a5364c-f295-4141-9b0f-3e54ba479d68', 'UNI-T30', 'chore', 'Unity.Chain', 71051, '2026-10-23T22:44:28.914Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a0218471269176675', 'U.RON-T11', 'docs', 'Unity.Cursory', 42979, '2026-10-22T10:05:31.249Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a092863282cba7e94', 'UC-T25', 'fix', 'Unity.Cursory', 32282, '2026-10-21T19:22:30.824Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a0c60bb7306e1f906', 'UC-T28', 'fix', 'Unity.Cursory', 27081, '2026-10-21T19:33:35.973Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a1b0371f4e5dae2d1', 'U.RON-T13', 'docs', 'Unity.Cursory', 25967, '2026-10-22T10:06:49.983Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a3b0e23e39d9e2f31', 'GG-T184', 'feature', 'Unity.Cursory', 286572, '2026-10-22T10:32:57.108Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a440e259ad0efb76f', 'UC-T27', 'feature', 'Unity.Cursory', 93032, '2026-10-21T19:40:28.299Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a4a5a8f1bcfd588b0', 'GG-T185', 'feature', 'Unity.Cursory', 103893, '2026-10-22T10:15:58.556Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a4c73ad8e317f58fc', 'UC-T30', 'fix', 'Unity.Cursory', 48402, '2026-10-21T19:47:08.976Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a67c847290bcaeadb', 'U.RON-T8', 'chore', 'Unity.Cursory', 92731, '2026-10-22T04:57:09.007Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a71d41292917dc3e6', 'UC-T15', 'docs', 'Unity.Cursory', 123835, '2026-10-21T20:14:15.451Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a792fa1ddb9711341', 'GG-T182', 'feature', 'Unity.Cursory', 585436, '2026-10-22T07:12:13.669Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-a9f6081cdeca5cd1e', 'GG-T186', 'feature', 'Unity.Cursory', 225349, '2026-10-22T10:39:08.67Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-aa7b904b2699bc519', 'GG-T181', 'feature', 'Unity.Cursory', 262680, '2026-10-22T06:05:50.074Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-ae8374d4c84325176', 'UC-T33', 'feature', 'Unity.Cursory', 589975, '2026-10-22T04:38:06.804Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c/agent-afdbeca983253dfbd', 'GG-T183', 'feature', 'Unity.Cursory', 441789, '2026-10-22T09:33:19.132Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('57099dc3-934a-43df-8002-7585af89c8f4', 'M.ORCH-T676', 'fix', 'MindAttic.Orchestrator', 10346, '2026-10-23T16:36:05.589Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('57c77d99-8c11-482d-a104-1f0fd0912870', 'TRC-T2', 'chore', 'TaxRateCollector', 111114, '2026-10-16T01:19:27.667Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5835c7b3-d394-497f-ac96-330be378f99c', 'RB-T162', 'fix', 'REA.Bloodlines', 31892, '2026-10-24T11:57:28.848Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5ad89c14-79aa-4653-8793-95a0890762e7', 'SS-T19', 'chore', 'Prose', 36900, '2026-10-15T11:56:21.312Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5c92ef58-99eb-4513-9909-e772f015b71b/agent-a188a02859b1db864', 'M.ORCH-T563', 'feature', 'MindAttic.Orchestrator', 1232587, '2026-10-21T04:22:58.897Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5c92ef58-99eb-4513-9909-e772f015b71b/agent-a55935eb35375cd96', 'M.ORCH-T565', 'fix', 'MindAttic.Orchestrator', 549727, '2026-10-21T04:01:03.442Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5c92ef58-99eb-4513-9909-e772f015b71b/agent-a5ae39e671dacb4d3', 'M.ORCH-T559', 'feature', 'MindAttic.Orchestrator', 741783, '2026-10-21T04:08:37.086Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5c92ef58-99eb-4513-9909-e772f015b71b/agent-ae43bd4b9806805d2', 'M.ORCH-T561', 'feature', 'MindAttic.Orchestrator', 164876, '2026-10-21T03:29:33.831Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5d708d0d-6544-4fba-86e3-cc7ef9e2151a', 'UNI-T16', 'fix', 'Unity.Chain', 37412, '2026-10-23T10:48:59.596Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-a2bcce1dd01fc15d5', 'UQD-T30', 'chore', 'Unity.QuickDuel', 769163, '2026-10-24T12:32:22.676Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-a40c2c26f8b043a9f', 'UQD-T31', 'chore', 'Unity.QuickDuel', 841998, '2026-10-24T14:12:00.112Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-a576746d3be42b64c', 'UQD-T33', 'feature', 'Unity.QuickDuel', 202260, '2026-10-24T14:56:05.069Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-a7a6659867d547050', 'UQD-T21', 'feature', 'Unity.QuickDuel', 298538, '2026-10-24T13:41:18.188Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-a80bb5ee92a45cc15', 'UQD-T32', 'feature', 'Unity.QuickDuel', 243909, '2026-10-24T14:40:40.169Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-a908eae2914cc7f7f', 'UQD-T29', 'chore', 'Unity.QuickDuel', 202248, '2026-10-24T11:41:23.687Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5df460c0-3934-4085-87b6-a12131b9f9e5/agent-ac6d93e7d827ca296', 'UQD-T34', 'feature', 'Unity.QuickDuel', 272487, '2026-10-24T15:26:40.586Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5e352c10-e91b-475f-86a1-fdd011baed35', 'MAO-T79', 'feature', 'MindAttic.Orchestrator', 5921453, '2026-10-15T09:11:58.222Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5e6ed18f-b847-44e8-b524-b16363a21d3c', 'UQD-T24', 'fix', 'Unity.QuickDuel', 16379, '2026-10-23T10:44:09.677Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5e95c216-37bf-4ec9-abea-6ae6d3d089fd', 'MB-T3', 'chore', 'MediaButler', 169274, '2026-10-16T01:45:49.201Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('60a2c669-6d1e-4de2-b1d8-1660f771603e', 'TUT-T21', 'docs', 'Tutor', 18296, '2026-10-21T18:11:48.154Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('61d5cec2-4d9a-40fa-b33e-7db0ca79379e', 'M.ORCH-T736', 'fix', 'MindAttic.Orchestrator', 110965, '2026-10-23T20:23:38.288Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('630533bf-8238-4561-b8fb-7093eef2a447', 'SS-T67', 'chore', 'Prose', 533913, '2026-10-15T18:08:08.638Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('63400c86-5a2d-4fb7-8571-391e791b4834', 'M.LNCH-T42', 'chore', 'MindAttic.Launcher', 20796, '2026-10-22T17:06:37.941Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('64fe7bd3-2e58-4ed4-9a5e-37c2828926fd', 'M.LNCH-T11', 'chore', 'MindAttic.Launcher', 27148, '2026-10-23T19:16:38.356Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('654a5400-904e-407f-8e50-e5a9693917d3', 'UR-T34', 'chore', 'Unity.Ricochet', 17686, '2026-10-23T23:40:15.878Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66101f61-5f6c-4ef1-b084-e8150cfc70f9', 'UNI-T44', 'fix', 'Unity.Chain', 50776, '2026-10-23T22:51:11.728Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6626b1f2-94b8-42ca-bc1f-d3df1b55c347', 'RB-T166', 'fix', 'REA.Bloodlines', 24710, '2026-10-24T11:46:24.146Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66709a05-e525-42dc-831d-a3dfb9f23234', 'M.TIX-T119', 'docs', 'MindAttic.Tickets', 19492, '2026-10-24T01:07:36.67Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a030a11dcbbf068b1', 'U.SPC-T262', 'chore', 'UnitySolarSystem', 47513, '2026-10-19T02:46:37.963Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a2371e4296b9363dd', 'U.SPC-T90', 'feature', 'UnitySolarSystem', 750543, '2026-10-19T07:04:22.96Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a477c6cf2b63727e9', 'U.SPC-T290', 'fix', 'UnitySolarSystem', 370115, '2026-10-19T07:26:32.402Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a666bf147279aaf7c', 'U.SPC-T97', 'feature', 'UnitySolarSystem', 741423, '2026-10-19T08:17:16.635Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a6abf9500711e74b0', 'U.SPC-T94', 'feature', 'UnitySolarSystem', 1480912, '2026-10-19T08:42:24.345Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a738e4f4089d6e051', 'U.SPC-T103', 'feature', 'UnitySolarSystem', 594598, '2026-10-19T08:07:28.14Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a7adeace36a592783', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 2729267, '2026-10-19T06:12:28.188Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a82feb35f83fc0899', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 350148, '2026-10-19T15:22:05.821Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-a9d0a2171fd89d059', 'U.SPC-T98', 'feature', 'UnitySolarSystem', 1058700, '2026-10-19T08:03:58.523Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-aa26767fe4e15f2a9', 'U.SPC-T91', 'feature', 'UnitySolarSystem', 580045, '2026-10-19T07:34:40.703Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-aa4cd1264d95a961b', 'U.SPC-T294', 'fix', 'UnitySolarSystem', 118533, '2026-10-19T14:54:37.848Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-aab18138ceaed8ccd', 'U.SPC-T92', 'feature', 'UnitySolarSystem', 1117815, '2026-10-19T08:26:21.271Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-ab509cfba9db0b702', 'U.SPC-T104', 'feature', 'UnitySolarSystem', 463474, '2026-10-19T03:47:05.879Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-ae63ec997f3bded11', 'U.SPC-T291', 'fix', 'UnitySolarSystem', 112528, '2026-10-19T07:47:26.832Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-ae8bde54c45d452c8', 'U.SPC-T93', 'feature', 'UnitySolarSystem', 438876, '2026-10-19T15:05:09.117Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('669c23d1-3f55-4799-bdaa-ecd8351ff01e/agent-af00ae1ce1feca030', 'U.SPC-T293', 'fix', 'UnitySolarSystem', 722334, '2026-10-19T08:50:11.91Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a19e7d14363742504', 'M.LNCH-T62', 'fix', '(workspace root)', 32012, '2026-10-22T17:07:05.354Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a1b74171a3f07aacd', 'M.LNCH-T65', 'feature', '(workspace root)', 110782, '2026-10-22T17:51:27.284Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a257f994ee4d7894a', 'M.LNCH-T46', 'chore', '(workspace root)', 25191, '2026-10-22T17:06:58.311Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a294ea0874cb318a1', 'M.ORCH-T608', 'feature', '(workspace root)', 146807, '2026-10-22T17:19:55.883Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a2a239faad0c53625', 'M.ORCH-T615', 'feature', '(workspace root)', 131932, '2026-10-22T17:50:39.308Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a319427f8369dc1e8', 'M.COMP-T50', 'feature', '(workspace root)', 133173, '2026-10-22T21:43:48.516Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a3400c0d951f47ba8', 'M.ORCH-T625', 'fix', '(workspace root)', 41775, '2026-10-22T17:43:38.805Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a38ebff41956f847e', 'M.LNCH-T35', 'chore', '(workspace root)', 30357, '2026-10-22T17:06:10.267Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a39ddcad58499ed00', 'M.LNCH-T45', 'docs', '(workspace root)', 32665, '2026-10-22T17:08:32.84Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a4099d6b76947dfc1', 'M.LNCH-T44', 'feature', '(workspace root)', 72984, '2026-10-22T17:09:52.143Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a47aef92fd42b5fa0', 'M.ORCH-T607', 'feature', 'MindAttic.Orchestrator', 207622, '2026-10-22T17:10:01.961Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a58c3de2166930fb4', 'M.ORCH-T620', 'fix', '(workspace root)', 76241, '2026-10-22T17:32:32.064Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a70b94645b6698920', 'M.ORCH-T618', 'feature', '(workspace root)', 353861, '2026-10-22T17:21:44.622Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a847eb48e4521c6d9', 'M.LNCH-T56', 'feature', '(workspace root)', 74716, '2026-10-22T17:50:15.094Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a9539b2ae83ab974e', 'M.ORCH-T634', 'fix', '(workspace root)', 53035, '2026-10-22T21:37:45.183Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-a9be89fcb64d2f191', 'M.LNCH-T64', 'feature', '(workspace root)', 54515, '2026-10-22T17:31:01.565Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-aa50582182190c737', 'M.LNCH-T33', 'chore', '(workspace root)', 143440, '2026-10-22T17:05:18.662Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-aa8bb465d5988f5af', 'M.ORCH-T614', 'feature', '(workspace root)', 176625, '2026-10-22T17:15:58.198Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-aab56ae00c9f85f08', 'M.ORCH-T635', 'fix', '(workspace root)', 45164, '2026-10-22T21:36:16.059Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-ab86aae654e8f2750', 'M.ORCH-T610', 'feature', 'MindAttic.Orchestrator', 183335, '2026-10-22T17:11:48.904Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-aba7ab152c977cd1c', 'M.ORCH-T624', 'fix', '(workspace root)', 34010, '2026-10-22T17:41:06.234Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-abdd22b22f87ee195', 'M.ORCH-T622', 'feature', '(workspace root)', 188808, '2026-10-22T17:55:03.185Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-ac0a2ff79dfdab254', 'M.COMP-T41', 'docs', '(workspace root)', 27309, '2026-10-22T21:34:26.288Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-ac551623d13727ffa', 'M.LNCH-T54', 'feature', '(workspace root)', 140250, '2026-10-22T17:20:57.024Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-acba5e670b37049c3', 'M.ORCH-T613', 'feature', '(workspace root)', 387508, '2026-10-22T17:37:42.065Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-ade779e91d17da312', 'M.ORCH-T628', 'feature', 'MindAttic.Orchestrator', 176268, '2026-10-22T21:16:04.851Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-adf602b04cf056109', 'M.LNCH-T55', 'feature', '(workspace root)', 94433, '2026-10-22T17:32:43.042Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-ae4ee5fe86a057504', 'M.ORCH-T611', 'fix', 'MindAttic.Orchestrator', 51544, '2026-10-22T17:01:10.69Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-aecd62cacd1c0e083', 'M.LNCH-T34', 'chore', '(workspace root)', 23534, '2026-10-22T17:05:42.622Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-aecebd1d47c02085a', 'M.LNCH-T42', 'chore', '(workspace root)', 45480, '2026-10-22T17:07:39.056Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-af5ef547444566e14', 'M.ORCH-T623', 'fix', '(workspace root)', 49805, '2026-10-22T17:35:06.804Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f/agent-afdd6190562333916', 'M.ORCH-T621', 'fix', '(workspace root)', 42825, '2026-10-22T17:31:09.25Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('66e645fc-c38f-40ca-9503-c928e0392c42', 'RB-T159', 'fix', 'REA.Bloodlines', 182704, '2026-10-24T00:10:22.683Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('678ee873-9405-4739-8cd4-93adf229744f', 'UNI-T28', 'fix', 'Unity.Chain', 56935, '2026-10-23T22:17:55.856Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6889017a-4db9-468d-b7d4-c9e74da50582', 'U.SPC-T40', 'feature', 'UnitySolarSystem', 160642, '2026-10-15T11:21:34.158Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a521cf2a262b93920', 'M.FLOW-T116', 'fix', 'MindAttic.Workflow', 119027, '2026-10-20T12:53:56.4Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a5d092fcc8805b2c5', 'M.FLOW-T63', 'feature', 'MindAttic.Workflow', 91537, '2026-10-20T04:26:56.423Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a70b837f8248aa123', 'M.FLOW-T61', 'chore', 'MindAttic.Workflow', 139805, '2026-10-20T04:29:46.822Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-a93603d764b18d87d', 'M.FLOW-T96', 'fix', 'MindAttic.Workflow', 47899, '2026-10-20T04:25:20Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-ab60294a40cd9d5d8', 'M.FLOW-T60', 'chore', 'MindAttic.Workflow', 14115, '2026-10-20T04:23:20.293Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6972d646-5793-48d8-bf2f-8dfaea97faa6/agent-ac6326db75e5b5f68', 'M.FLOW-T117', 'feature', 'MindAttic.Workflow', 96405, '2026-10-20T12:52:18.295Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('699beb8c-d2fb-4cb8-b03b-a14457a3116b', 'MAT-T16', 'fix', 'MindAttic.Agency', 19649, '2026-10-24T04:54:38.984Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-a1fd3fa7d2276e6fc', 'UQD-T22', 'fix', 'Unity.QuickDuel', 126054, '2026-10-23T09:37:59.341Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-a26b713b6d6c893a6', 'UQD-T23', 'fix', 'Unity.QuickDuel', 240472, '2026-10-23T10:56:25.355Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-a4e020fbbaf58fd9b', 'UQD-T20', 'fix', 'Unity.QuickDuel', 63701, '2026-10-23T06:45:17.692Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-a58b6bd4ec39e8e1b', 'UQD-T25', 'fix', 'Unity.QuickDuel', 142166, '2026-10-23T11:22:53.389Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-a5eabccf63322558b', 'UQD-T19', 'feature', 'Unity.QuickDuel', 146899, '2026-10-23T08:45:16.09Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-a996aafb7f35dad05', 'UQD-T24', 'fix', 'Unity.QuickDuel', 146094, '2026-10-23T10:48:08.099Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('69dd2e90-8537-4f86-be6d-004afc057e14/agent-ad47eb2c33dbd5ce1', 'UQD-T14', 'feature', 'Unity.QuickDuel', 167918, '2026-10-23T05:48:30.59Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6a4d0ff5-3183-4d0a-a3c7-f261ff488501', 'UH-T39', 'chore', 'Unity.Hare', 172093, '2026-10-24T00:40:07.28Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a248aff33f9a78940', 'U.SPC-T67', 'feature', 'UnitySolarSystem', 394005, '2026-10-17T03:52:56.514Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a2b545b96756abf2b', 'U.SPC-T59', 'feature', 'UnitySolarSystem', 311132, '2026-10-17T00:08:12.489Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a2ff1120dcf4740bc', 'U.SPC-T101', 'feature', 'UnitySolarSystem', 1039085, '2026-10-17T10:06:17.998Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a31c46f81e988e100', 'U.SPC-T104', 'feature', 'UnitySolarSystem', 74207, '2026-10-17T04:54:04.286Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a3f00bc7b6f3cbe37', 'U.SPC-T40', 'feature', 'UnitySolarSystem', 209147, '2026-10-17T13:47:14.976Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a41601f3b1a1b6e8e', 'U.SPC-T58', 'feature', 'UnitySolarSystem', 360990, '2026-10-17T00:27:18.326Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a4e48e57239a39781', 'U.SPC-T96', 'feature', 'UnitySolarSystem', 1915674, '2026-10-17T09:10:59.647Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a5cdb652d446481d6', 'U.SPC-T100', 'feature', 'UnitySolarSystem', 602908, '2026-10-17T09:37:51.971Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a76fb1ef36ffec922', 'U.SPC-T95', 'feature', 'UnitySolarSystem', 2493712, '2026-10-17T05:39:01.129Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-a913d0f8bfa261775', 'U.SPC-T57', 'feature', 'UnitySolarSystem', 582025, '2026-10-17T00:16:46.102Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-aa25fbd0fea58430f', 'U.SPC-T41', 'feature', 'UnitySolarSystem', 685454, '2026-10-17T14:03:34.648Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ab1481e4bfd4f9d17', 'U.SPC-T65', 'feature', 'UnitySolarSystem', 325573, '2026-10-17T02:17:05.132Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ab87b89b554c5e5b9', 'U.SPC-T42', 'feature', 'UnitySolarSystem', 236870, '2026-10-17T14:22:15.16Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-aba751382a6b59cdd', 'U.SPC-T61', 'feature', 'UnitySolarSystem', 410142, '2026-10-17T01:23:43.682Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-abc1f19080ddf9728', 'U.SPC-T102', 'feature', 'UnitySolarSystem', 1058338, '2026-10-17T10:17:50.618Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-abe0ed789b40f45a8', 'U.SPC-T182', 'fix', 'UnitySolarSystem', 523056, '2026-10-17T14:59:49.212Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ac1cb2b5b860b4396', 'U.SPC-T178', 'fix', 'UnitySolarSystem', 235109, '2026-10-17T06:05:09.555Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ac29b76500799739c', 'U.SPC-T62', 'feature', 'UnitySolarSystem', 1417093, '2026-10-17T04:01:23.757Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-acaaefff52a93b7a2', 'U.SPC-T45', 'feature', 'UnitySolarSystem', 326875, '2026-10-16T23:10:06.047Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ad7b971363bd201af', 'U.SPC-T79', 'fix', 'UnitySolarSystem', 48996, '2026-10-16T22:36:23.69Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-adbcc8d01720481f8', 'U.SPC-T56', 'feature', 'UnitySolarSystem', 283973, '2026-10-16T23:45:25.41Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-ae84c9a48ce37a677', 'U.SPC-T66', 'feature', 'UnitySolarSystem', 304719, '2026-10-17T02:54:55.938Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-aecffc8f2ea4d13ad', 'U.SPC-T60', 'feature', 'UnitySolarSystem', 885678, '2026-10-17T01:50:12.709Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af11268c85885b0e5', 'U.SPC-T64', 'feature', 'UnitySolarSystem', 332052, '2026-10-17T01:34:05.265Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af3207b5845435ec5', 'U.SPC-T63', 'feature', 'UnitySolarSystem', 1316944, '2026-10-17T08:13:54.732Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af48d353f97a9d18c', 'U.SPC-T55', 'feature', 'UnitySolarSystem', 389307, '2026-10-16T23:21:04.795Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af8687d63aed431cf', 'U.SPC-T99', 'feature', 'UnitySolarSystem', 2680350, '2026-10-17T08:29:06.38Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6be4d45b-4ddc-4845-b42e-3b4847e1c10f/agent-af9fb1dda2b9b430a', 'U.SPC-T49', 'chore', 'UnitySolarSystem', 2753723, '2026-10-17T13:29:39.304Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6c05b7c4-7cbe-4503-bed2-2a1b28f2e17d/agent-a1c8110db31e47875', 'PRS-T136', 'docs', 'Prose', 113862, '2026-10-19T20:07:26.984Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6ccaa65c-1a0d-48a2-81e8-cbebfbc2cb49', 'RB-T137', 'fix', 'REA.Bloodlines', 45475, '2026-10-23T13:59:25.089Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6d72b6f5-bc49-4578-ba97-2e3d3444af03', 'M.FLOW-T14', 'chore', 'MindAttic.Workflow', 231677, '2026-10-15T07:26:15.21Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6d844106-bcc9-4932-8fe2-4248e6ac9502', 'M.ORCH-T671', 'fix', 'MindAttic.Orchestrator', 14063, '2026-10-23T16:15:29.045Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a2471a89b9e63fcd0', 'GG-T93', 'feature', 'GridGameGaiden', 688743, '2026-10-19T16:30:14.633Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a30b3ba3072f7dc95', 'GG-T96', 'feature', 'GridGameGaiden', 344208, '2026-10-19T16:42:03.04Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a327f267b6975d224', 'GG-T95', 'feature', 'GridGameGaiden', 749888, '2026-10-19T17:08:55.31Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a339380d04380ad31', 'GG-T112', 'fix', 'GridGameGaiden', 113703, '2026-10-19T20:42:44.184Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a35e7ff5b3b9ce064', 'GG-T100', 'fix', 'GridGameGaiden', 471087, '2026-10-19T18:34:12.04Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a4a0b93e9dfe32a74', 'GG-T72', 'chore', 'GridGameGaiden', 125673, '2026-10-19T15:59:18.475Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a508ceafe251e42b8', 'GG-T121', 'fix', 'GridGameGaiden', 816239, '2026-10-19T21:32:45.226Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a54055289a7fcbb5b', 'GG-T73', 'docs', 'GridGameGaiden', 100785, '2026-10-19T15:18:39.638Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a67ae412e6356d228', 'GG-T97', 'fix', 'GridGameGaiden', 832791, '2026-10-19T19:28:07.007Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a716dd0490796b7cb', 'GG-T84', 'fix', 'GridGameGaiden', 128833, '2026-10-19T15:45:07.126Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-a8a1613d6f571ec83', 'GG-T87', 'fix', 'GridGameGaiden', 166274, '2026-10-19T16:09:21.555Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aa53c02aec03f1431', 'GG-T70', 'chore', 'GridGameGaiden', 469118, '2026-10-19T16:50:37.97Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aa67d4518225c45e9', 'GG-T118', 'feature', 'GridGameGaiden', 1065049, '2026-10-19T21:44:21.372Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aae07a364b856b900', 'GG-T117', 'feature', 'GridGameGaiden', 284238, '2026-10-19T20:14:38.639Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ab77059f349a4d543', 'GG-T114', 'fix', 'GridGameGaiden', 256816, '2026-10-19T20:52:23.374Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ab837b7bdd5cc65c6', 'MAU-T2', 'feature', 'GridGameGaiden', 72635, '2026-10-19T20:32:10.647Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-aca72e0e79147d144', 'GG-T98', 'fix', 'GridGameGaiden', 258853, '2026-10-19T17:36:53.57Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ad2d52dcf85c75880', 'GG-T119', 'feature', 'GridGameGaiden', 2211974, '2026-10-19T22:15:22.668Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ad49332e77f654c6d', 'GG-T110', 'fix', 'GridGameGaiden', 769664, '2026-10-19T19:19:08.033Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-ad58acb77ce3d9adc', 'GG-T126', 'fix', 'GridGameGaiden', 563129, '2026-10-19T20:28:07.785Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6daaf6b3-afd1-4b34-a6ec-a195670eef3e/agent-af180004d9fe18d58', 'GG-T129', 'fix', 'GridGameGaiden', 604165, '2026-10-19T21:09:13.906Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6e4994f9-ce65-4ccf-8d32-0dc48241925d', 'GG-T43', 'feature', 'GridGameGaiden', 430422, '2026-10-15T14:46:49.253Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86', 'RB-T135', 'fix', 'REA.Bloodlines', 5314123, '2026-10-23T02:42:27.997Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-a08eddff244349c6d', 'RB-T93', 'fix', 'REA.Bloodlines', 231127, '2026-10-23T00:57:53.486Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-a52da067497cc1912', 'RB-T88', 'fix', 'REA.Bloodlines', 294512, '2026-10-22T23:06:09.436Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-a5e7ecd3e2e41686b', 'RB-T90', 'fix', 'REA.Bloodlines', 358690, '2026-10-22T23:30:52.235Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-aa52dcf8ffc6ca57d', 'RB-T89', 'fix', 'REA.Bloodlines', 320558, '2026-10-22T23:35:54.292Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-abbad92ac9057b92a', 'RB-T92', 'fix', 'REA.Bloodlines', 465558, '2026-10-23T00:37:23.574Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-ac7853a18198ac7e1', 'RB-T94', 'fix', 'REA.Bloodlines', 242178, '2026-10-23T02:15:38.866Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('703a0095-0c8e-4b3f-8af9-9c845b243c86/agent-aec918a1c49aec8d5', 'RB-T91', 'fix', 'REA.Bloodlines', 241699, '2026-10-23T00:09:11.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a0caa61c9dfc286f1', 'G.AWPN-T187', 'fix', 'GTFO.EZ', 21868, '2026-10-20T13:24:11.576Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a199fabc16ea03384', 'GAM-T41', 'feature', 'GTFO.EZ', 40935, '2026-10-20T04:24:34.686Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a1bec0b133536e40e', 'GAI-T14', 'fix', 'GTFO.EZ', 36786, '2026-10-20T04:24:29.227Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a25f79a47c184b03b', 'G.SD-T4', 'chore', 'GTFO.EZ', 16230, '2026-10-20T04:23:17.802Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a26b59b6f5af81025', 'G.AWPN-T188', 'fix', 'GTFO.EZ', 29593, '2026-10-20T13:25:17.693Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a2d5eac1cdaeeec2d', 'G.CTD-T4', 'chore', 'GTFO.EZ', 15844, '2026-10-20T04:22:45.478Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a329025e8f0fbd9fc', 'GAI-T15', 'fix', 'GTFO.EZ', 43633, '2026-10-20T04:24:42.334Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a3e6bcf2658530e63', 'G.SD-T5', 'chore', 'GTFO.EZ', 36657, '2026-10-20T04:24:41.304Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a5da9ef7206e599fb', 'G.AWPN-T8', 'chore', 'GTFO.EZ', 424670, '2026-10-20T13:56:32.524Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-a7376f2f0bb2f4bf3', 'G.AWPN-T10', 'chore', 'GTFO.EZ', 131690, '2026-10-20T13:01:10.396Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aa11e74e2f7492d88', 'G.AWPN-T210', 'fix', 'GTFO.EZ', 38285, '2026-10-20T13:51:00.315Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ab36b110de08b01d9', 'G.CTD-T6', 'docs', 'GTFO.EZ', 61134, '2026-10-20T04:24:00.52Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ab7b8f385c3cc22db', 'G.CTD-T3', 'chore', 'GTFO.EZ', 114337, '2026-10-20T04:27:09.129Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ab82ebbc97a3b858e', 'G.CTD-T5', 'chore', 'GTFO.EZ', 89542, '2026-10-20T04:25:17.668Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ac2a22bc5753c1da3', 'G.AWPN-T189', 'fix', 'GTFO.EZ', 47764, '2026-10-20T13:26:11.305Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aca711b737d5fd90e', 'G.AWPN-T209', 'fix', 'GTFO.EZ', 38325, '2026-10-20T13:40:50.413Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-acb06516c6c4e7ab2', 'G.AWPN-T9', 'chore', 'GTFO.EZ', 16943, '2026-10-20T12:54:11.5Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ad157af0a5e1db80c', 'G.AWPN-T89', 'chore', 'GTFO.EZ', 22768, '2026-10-20T12:54:14.763Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-ad9fb7d354011067d', 'G.AWPN-T208', 'fix', 'GTFO.EZ', 39930, '2026-10-20T13:41:01.757Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aeb834d6f88b1c5ed', 'G.SD-T6', 'docs', 'GTFO.EZ', 47794, '2026-10-20T04:23:56.116Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-aecbbc0cd280dc8b7', 'G.SD-T3', 'chore', 'GTFO.EZ', 39441, '2026-10-20T04:23:44.951Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71a98525-34a0-4732-97d8-8a771755e7fe/agent-af0cdc82875af8d06', 'G.AWPN-T186', 'fix', 'GTFO.EZ', 45089, '2026-10-20T12:55:39.177Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a15e39cfaf1725d03', 'UM-T6', 'feature', '(workspace root)', 147765, '2026-10-20T18:33:27.506Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a1a3046942864ec88', 'UC-T5', 'feature', '(workspace root)', 231704, '2026-10-20T20:49:40.485Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a1a4300f935be8b5d', 'UC-T3', 'chore', '(workspace root)', 130823, '2026-10-20T19:46:00.344Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a1fbee5b56bf0cee5', 'UM-T12', 'feature', '(workspace root)', 168913, '2026-10-20T20:39:36.268Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a21acede47ca29b9c', 'UM-T9', 'feature', '(workspace root)', 308157, '2026-10-20T19:24:14.358Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a3d16eb0826776aa8', 'UM-T15', 'feature', '(workspace root)', 1036348, '2026-10-20T22:52:07.156Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a516930353fd2c89e', 'UM-T7', 'feature', '(workspace root)', 143825, '2026-10-20T18:50:18.916Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a52e76a4f59a06c00', 'UM-T14', 'feature', '(workspace root)', 382448, '2026-10-20T21:20:57.655Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a59c230ff3be02374', 'UM-T23', 'feature', '(workspace root)', 402875, '2026-10-21T00:42:17.02Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a6af54e56c3e7a249', 'UC-T6', 'feature', '(workspace root)', 167432, '2026-10-20T20:58:20.276Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a8dd1a0a9175a15fd', 'UM-T11', 'feature', '(workspace root)', 268809, '2026-10-20T19:52:06Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a9850620f4fce731d', 'UM-T3', 'chore', '(workspace root)', 85488, '2026-10-20T17:51:48.473Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-a9b304cc95621d45d', 'UM-T5', 'feature', '(workspace root)', 112937, '2026-10-20T18:20:30.049Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-aac7d8be8748297d7', 'UM-T10', 'feature', '(workspace root)', 318321, '2026-10-20T19:39:06.96Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-ab33f3b06943e0c25', 'UM-T4', 'feature', '(workspace root)', 157589, '2026-10-20T18:14:47.88Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-ace7fcd44179e4444', 'UM-T34', 'fix', '(workspace root)', 117527, '2026-10-20T21:26:47.468Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-ae58fc2124cc06628', 'UM-T8', 'feature', '(workspace root)', 259015, '2026-10-20T19:09:07.122Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-af132a04ecee6322b', 'UM-T16', 'feature', '(workspace root)', 372648, '2026-10-21T00:23:24.838Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-afae78455dda9332f', 'UC-T4', 'feature', '(workspace root)', 146078, '2026-10-20T20:36:34.306Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('71ffc829-973e-41e6-bfe0-95e4e7759eff/agent-aff229900c5f15281', 'UM-T13', 'feature', '(workspace root)', 347207, '2026-10-20T20:59:26.226Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('727c3e91-894d-40e1-80ee-8c4a123538f3', 'SS-T85', 'chore', 'Prose', 739134, '2026-10-15T20:48:34.45Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('72d2c115-e155-4990-9252-bfd323471271', 'U.SPC-T34', 'chore', 'UnitySolarSystem', 120179, '2026-10-15T10:53:46.527Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('731526e1-6872-48cb-9a71-70c07b4cf5fc', 'RB-T173', 'fix', 'REA.Bloodlines', 47416, '2026-10-24T12:27:25.51Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('73cba3d1-c191-4012-a599-d842e95c6850', 'UR-T23', 'fix', 'Unity.Ricochet', 22941, '2026-10-23T22:18:56.091Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('73e45d1b-b1ba-4183-8bc8-ad138e00ac90', 'GG-T55', 'feature', 'GridGameGaiden', 245815, '2026-10-15T15:49:44.573Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a051d348d96ade1e2', 'M.ORCH-T661', 'feature', 'MindAttic.Orchestrator', 67624, '2026-10-23T17:06:05.902Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a05fbd55bff4c7e75', 'M.ORCH-T686', 'feature', 'MindAttic.Orchestrator', 213874, '2026-10-23T18:13:06.075Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a1bdb1cf065860222', 'M.LNCH-T36', 'docs', 'MindAttic.Orchestrator', 55960, '2026-10-23T19:14:12.552Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a2889e1867b161dc4', 'M.ORCH-T694', 'feature', 'MindAttic.Orchestrator', 307141, '2026-10-23T19:24:37.526Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a34e7939919621072', 'M.LNCH-T67', 'feature', 'MindAttic.Orchestrator', 84031, '2026-10-23T17:00:39.732Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a44f0bfde4446a844', 'M.ORCH-T698', 'fix', 'MindAttic.Orchestrator', 58720, '2026-10-23T19:30:15.802Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a52a5c3032e1c3f04', 'M.ORCH-T682', 'feature', 'MindAttic.Orchestrator', 407813, '2026-10-23T17:44:36.103Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a539346f50b927ecb', 'M.ORCH-T681', 'feature', 'MindAttic.Orchestrator', 156132, '2026-10-23T17:09:12.501Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a58e0f180b303e00a', 'M.COMP-T51', 'fix', 'MindAttic.Orchestrator', 45428, '2026-10-23T15:51:42.905Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a678e862d0b9a80ba', 'M.ORCH-T670', 'feature', 'MindAttic.Orchestrator', 172347, '2026-10-23T16:09:30.478Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a76c7b5df84c01969', 'M.COMP-T52', 'feature', 'MindAttic.Orchestrator', 340488, '2026-10-23T16:04:31.167Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a81ef6adb7660cb45', 'M.ORCH-T687', 'fix', 'MindAttic.Orchestrator', 115659, '2026-10-23T18:18:40.303Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a887cf2233df6f4c1', 'M.ORCH-T669', 'feature', 'MindAttic.Orchestrator', 263151, '2026-10-23T16:58:12.966Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-a97e78ffaa3bffa4c', 'M.ORCH-T672', 'fix', 'MindAttic.Orchestrator', 89838, '2026-10-23T16:31:37.924Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ab2ef34ef6079c21b', 'M.ORCH-T673', 'fix', 'MindAttic.Orchestrator', 80482, '2026-10-23T16:34:24.327Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ab2f0b5df89742ec0', 'M.COMP-T53', 'feature', 'MindAttic.Orchestrator', 188174, '2026-10-23T16:53:29.457Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ab5ceb9c7b8b1280f', 'M.ORCH-T678', 'feature', 'MindAttic.Orchestrator', 224277, '2026-10-23T17:07:07.829Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ac5dd4d242e5b1596', 'M.ORCH-T685', 'feature', 'MindAttic.Orchestrator', 265680, '2026-10-23T18:00:20.982Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ac8010d6d002fa6f6', 'M.ORCH-T666', 'feature', 'MindAttic.Orchestrator', 618821, '2026-10-23T16:43:30.433Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ac9e1ddd6da57be36', 'M.ORCH-T675', 'fix', 'MindAttic.Orchestrator', 30173, '2026-10-23T16:30:55.163Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-acae5e9a42151c2a8', 'M.ORCH-T684', 'fix', 'MindAttic.Orchestrator', 140861, '2026-10-23T17:50:23.111Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ad23f69b32d8e5c5b', 'M.ORCH-T679', 'fix', 'MindAttic.Orchestrator', 46738, '2026-10-23T16:53:25.939Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ae36925bb21283137', 'M.ORCH-T680', 'feature', 'MindAttic.Orchestrator', 373231, '2026-10-23T17:15:15.858Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-ae47369cd1f55ff0e', 'M.ORCH-T676', 'fix', 'MindAttic.Orchestrator', 64937, '2026-10-23T16:40:33.754Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-af0825d969b90098e', 'M.ORCH-T668', 'feature', 'MindAttic.Orchestrator', 657704, '2026-10-23T16:39:57.555Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-af41e5f1777c68067', 'M.ORCH-T664', 'feature', 'MindAttic.Orchestrator', 201222, '2026-10-23T16:23:44.301Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-af4a48da893ca6bc5', 'M.ORCH-T667', 'feature', 'MindAttic.Orchestrator', 504495, '2026-10-23T16:49:35.769Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('74af4948-b4f1-4a22-8e0b-5ba1bf252736/agent-afc3a54d93ce54888', 'M.ORCH-T671', 'fix', 'MindAttic.Orchestrator', 86843, '2026-10-23T16:16:38.114Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('76d54fd4-c346-437c-936d-f61f475d1d9c', 'GG-T44', 'feature', 'GridGameGaiden', 233632, '2026-10-15T15:14:17.606Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('777043ff-2435-4c23-b5eb-f9dc26712a00', 'U.SPC-T31', 'chore', 'UnitySolarSystem', 147687, '2026-10-15T10:15:29.572Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7838b3d2-863c-414f-95e7-eaf3bb4b4ba0', 'SS-T77', 'chore', 'Prose', 1555326, '2026-10-15T20:48:04.151Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a0245fc7c2cf689aa', 'GG-T143', 'fix', 'GridGameGaiden', 75273, '2026-10-20T05:48:19.731Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a2ec44edd6170fc81', 'GG-T120', 'feature', 'GridGameGaiden', 690075, '2026-10-20T05:14:48.319Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a3ca4141fd935b73e', 'GG-T138', 'fix', 'GridGameGaiden', 342024, '2026-10-20T06:31:30.117Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a3d0d6b1e86d7a6d3', 'GG-T150', 'fix', 'GridGameGaiden', 50213, '2026-10-20T05:49:30.172Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a531b6464191877c5', 'GG-T140', 'fix', 'GridGameGaiden', 239844, '2026-10-20T06:12:16.41Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a611d9ad195ab10a1', 'GG-T154', 'fix', 'GridGameGaiden', 263150, '2026-10-20T06:51:47.115Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a761ba80c550c981e', 'GG-T148', 'fix', 'GridGameGaiden', 108272, '2026-10-20T06:13:01.934Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a7fb3f7ad428d7c8f', 'GG-T145', 'fix', 'GridGameGaiden', 149446, '2026-10-20T06:23:08.578Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a8488d9057f7953c3', 'GG-T141', 'fix', 'GridGameGaiden', 185637, '2026-10-20T06:15:42.321Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a8bdab32ffc666122', 'GG-T149', 'fix', 'GridGameGaiden', 138494, '2026-10-20T06:15:34.653Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a92520fef841c48f7', 'GG-T142', 'fix', 'GridGameGaiden', 102586, '2026-10-20T05:57:48.136Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a97a4fe041eca3e97', 'GG-T144', 'fix', 'GridGameGaiden', 65828, '2026-10-20T05:49:13.89Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a97fcde6c0512bff1', 'GG-T115', 'docs', 'GridGameGaiden', 289717, '2026-10-20T06:07:05.69Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a98c4a46d923e440e', 'GG-T139', 'fix', 'GridGameGaiden', 186364, '2026-10-20T06:18:56.951Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a998d27faa3c38c1d', 'GG-T146', 'fix', 'GridGameGaiden', 382311, '2026-10-20T06:31:27.904Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-a9a09476d6b733fd7', 'GG-T70', 'chore', 'GridGameGaiden', 3182079, '2026-10-20T07:10:10.446Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-aa37526fd7738cc1b', 'GG-T147', 'fix', 'GridGameGaiden', 156215, '2026-10-20T06:10:29.75Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-ac1914442c92d85e3', 'GG-T130', 'fix', 'GridGameGaiden', 251147, '2026-10-20T04:59:21.448Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-acc6be0aedda2f7ba', 'GG-T153', 'fix', 'GridGameGaiden', 200982, '2026-10-20T06:40:42.971Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78d34dfe-4731-4c14-98d5-5cca8f1882f8/agent-ae3b5713f94fc8a88', 'GG-T152', 'fix', 'GridGameGaiden', 166842, '2026-10-20T06:36:03.819Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('790ec0a7-a1db-4a71-b081-41e870622f4a', 'AWN-T3', 'chore', 'AutoWebNav', 66189, '2026-10-16T01:29:21.189Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7962698d-b245-4298-89b3-48e23f842740', 'M.ORCH-T641', 'fix', 'MindAttic.Orchestrator', 5863, '2026-10-22T23:55:21.043Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('796d8450-d5f1-42cb-ad1f-03135e9f34dc', 'SS-T61', 'chore', 'Prose', 254662, '2026-10-15T18:28:47.551Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7cd5ae65-619e-4bfd-a54f-9827e19b1f7d', 'GG-T25', 'chore', 'GridGameGaiden', 67707, '2026-10-16T01:28:57.456Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7d078a34-cf10-4349-bdf7-c5685e9b8731', 'M.BRWS-T9', 'docs', 'MindAttic.Browser', 15783, '2026-10-21T18:10:31.935Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7db50265-d3af-4335-bd88-75027f391494', 'SS-T94', 'chore', 'Prose', 237342, '2026-10-15T18:49:33.087Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7e5f7210-b66f-4380-96e6-2d80b00f88b3', 'UH-T74', 'fix', 'Unity.Hare', 40492, '2026-10-24T01:13:55.275Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7ed19d3c-0d29-476c-9750-f73011374001', 'SET-T10', 'docs', 'SyntheticExchangeTheory', 15172, '2026-10-21T18:10:41.956Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a017dc39c915b623e', 'RB-T54', 'chore', 'REA.Bloodlines', 177846, '2026-10-22T18:24:29.182Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a03c7058de7e5d74d', 'RB-T55', 'chore', 'REA.Bloodlines', 63549, '2026-10-22T18:14:34.3Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a11401cf3263bfffd', 'RB-T43', 'fix', 'REA.Bloodlines', 66515, '2026-10-22T15:04:53.721Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a12f755dfc0bf957d', 'RB-T51', 'feature', 'REA.Bloodlines', 355142, '2026-10-22T20:11:35.16Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a130695aaed03488a', 'RB-T120', 'chore', 'REA.Bloodlines', 77831, '2026-10-22T21:40:22.419Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a1a1a6036c0105f65', 'RB-T73', 'fix', 'REA.Bloodlines', 197967, '2026-10-22T21:36:08.365Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a2fb9c817618b562e', 'RB-T84', 'fix', 'REA.Bloodlines', 92965, '2026-10-22T21:46:18.31Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a321cee4848a0a366', 'RB-T37', 'feature', 'REA.Bloodlines', 4659961, '2026-10-22T18:26:48.294Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a39ebf4d41ffbe394', 'RB-T36', 'feature', 'REA.Bloodlines', 1253896, '2026-10-22T14:19:34.184Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a3a9ab7dec26a7b96', 'RB-T32', 'feature', 'REA.Bloodlines', 591278, '2026-10-22T14:11:23.638Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a464bbb8e66c2eee4', 'RB-T59', 'fix', 'REA.Bloodlines', 184237, '2026-10-22T21:34:27.303Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a589bd128939d0707', 'RB-T39', 'feature', 'REA.Bloodlines', 304354, '2026-10-22T15:03:24.268Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a640ac1b58a3c60f4', 'RB-T50', 'feature', 'REA.Bloodlines', 576842, '2026-10-22T21:16:39.132Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a64b542ab14e59220', 'RB-T78', 'fix', 'REA.Bloodlines', 251515, '2026-10-22T21:34:00.948Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a7549b51b5cf0dc33', 'RB-T81', 'fix', 'REA.Bloodlines', 112916, '2026-10-22T21:42:45.711Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a758b4223e91412b2', 'RB-T53', 'feature', 'REA.Bloodlines', 205963, '2026-10-22T21:16:42.548Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a8436d956b6d604fe', 'RB-T48', 'feature', 'REA.Bloodlines', 755096, '2026-10-22T21:39:40.58Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a8b4647d324b31836', 'RB-T46', 'fix', 'REA.Bloodlines', 66612, '2026-10-22T18:15:03.069Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a8b509e67e1003db8', 'RB-T83', 'fix', 'REA.Bloodlines', 47044, '2026-10-22T21:37:04.812Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a8feeaf2623d50adb', 'RB-T34', 'feature', 'REA.Bloodlines', 491539, '2026-10-22T13:45:59.748Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a913fc2177129aafa', 'RB-T87', 'fix', 'REA.Bloodlines', 64977, '2026-10-22T21:42:56.918Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a943c35e891c81ea0', 'RB-T42', 'fix', 'REA.Bloodlines', 60596, '2026-10-22T14:36:13.333Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-a9a46790d8f1ede35', 'RB-T56', 'chore', 'REA.Bloodlines', 206473, '2026-10-22T18:34:19.866Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-aa59a3ad0185be0e7', 'RB-T47', 'feature', 'REA.Bloodlines', 566784, '2026-10-22T21:15:45.55Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-aaa12da6ea2990975', 'RB-T38', 'fix', 'REA.Bloodlines', 184096, '2026-10-22T14:09:57.912Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-ab5ac7cc51c08aa84', 'RB-T35', 'feature', 'REA.Bloodlines', 1601342, '2026-10-22T14:53:20.033Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-ab8c70c6f0f1edf46', 'RB-T45', 'fix', 'REA.Bloodlines', 62379, '2026-10-22T16:54:44.959Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-ab8d4ca75f1766f39', 'RB-T61', 'fix', 'REA.Bloodlines', 296605, '2026-10-22T21:59:07.698Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-ac2add5dc86094da7', 'RB-T85', 'fix', 'REA.Bloodlines', 115421, '2026-10-22T21:49:45.779Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-ac45f61261150a446', 'RB-T119', 'docs', 'REA.Bloodlines', 102816, '2026-10-22T20:24:28.424Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-acec089ad7a1960b2', 'RB-T49', 'feature', 'REA.Bloodlines', 318381, '2026-10-22T19:39:54.702Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-add2c3dbb0767f9f2', 'RB-T82', 'fix', 'REA.Bloodlines', 68764, '2026-10-22T21:41:35.034Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-af1d844efb9d69e38', 'RB-T52', 'feature', 'REA.Bloodlines', 413978, '2026-10-22T21:15:59.019Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7fb4df7f-f57a-4d06-abbc-8f7f03e9941d/agent-af576bb05a170a6aa', 'RB-T33', 'feature', 'REA.Bloodlines', 625357, '2026-10-22T13:52:42.089Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a01fea85087797205', 'RB-T114', 'feature', 'REA.Bloodlines', 696701, '2026-10-24T14:37:50.488Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a3050f8b171f1ff0e', 'RB-T98', 'feature', 'REA.Bloodlines', 495476, '2026-10-24T15:33:16.416Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a316d69b1ac85f2d4', 'RB-T175', 'fix', 'REA.Bloodlines', 850177, '2026-10-24T15:52:17.353Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a35cd50a415a76583', 'RB-T166', 'fix', 'REA.Bloodlines', 256968, '2026-10-24T11:51:49.887Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a4d3c9c5a46999887', 'RB-T115', 'feature', 'REA.Bloodlines', 1054611, '2026-10-24T16:07:26.174Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a6518ef7f847b82fb', 'RB-T117', 'feature', 'REA.Bloodlines', 578865, '2026-10-24T15:58:39.953Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a700cafa14d533e0e', 'RB-T171', 'fix', 'REA.Bloodlines', 185481, '2026-10-24T11:54:16.145Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a8f74d317890c7418', 'RB-T162', 'fix', 'REA.Bloodlines', 843998, '2026-10-24T14:51:24.568Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-a9342887829efa79c', 'RB-T147', 'fix', 'REA.Bloodlines', 140531, '2026-10-24T11:28:40.621Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-aa074728022b3e4e7', 'RB-T111', 'feature', 'REA.Bloodlines', 451774, '2026-10-24T14:06:08.823Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-aa104e123ff0bcc4c', 'RB-T169', 'fix', 'REA.Bloodlines', 458051, '2026-10-24T12:08:46.655Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-aac7870029b0f896b', 'RB-T174', 'fix', 'REA.Bloodlines', 599477, '2026-10-24T12:54:31.889Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-ab72d254df9397ea7', 'RB-T110', 'feature', 'REA.Bloodlines', 765941, '2026-10-24T13:22:28.709Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-ae1d1300d821fb74d', 'RB-T108', 'feature', 'REA.Bloodlines', 1162663, '2026-10-24T13:33:41.455Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-ae6d9a1d956019880', 'RB-T131', 'fix', 'REA.Bloodlines', 243985, '2026-10-24T11:29:47.814Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-ae9085919bd173e85', 'RB-T173', 'fix', 'REA.Bloodlines', 777630, '2026-10-24T12:35:58.42Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('80266bf8-e700-42a4-8046-431131f55d29/agent-afd1b3622748847d4', 'RB-T116', 'feature', 'REA.Bloodlines', 630540, '2026-10-24T15:54:19.862Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('81152999-dc47-438d-8eaa-4b0922c3ece2', 'M.ORCH-T698', 'fix', 'MindAttic.Orchestrator', 17222, '2026-10-23T19:29:29.15Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('81477b29-986f-4f88-8bc7-ac56739357e8', 'SS-T89', 'chore', 'Prose', 404902, '2026-10-15T20:48:50.114Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('814c87c9-9ba9-4755-b6e2-6153d761330f', 'RB-T42', 'fix', 'REA.Bloodlines', 8897, '2026-10-22T14:35:43.931Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('837350f4-4c30-4298-9a91-4a24d4fc7425', 'U.INC-T21', 'chore', 'Unity.Incognito', 241143, '2026-10-24T10:35:03.267Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('84815464-d977-4c85-8ce7-a7d07487de62', 'AUT-T5', 'chore', 'Automata', 155659, '2026-10-16T00:26:44.597Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a01d53b333e844d05', 'U.SPC-T323', 'fix', '(workspace root)', 1119184, '2026-10-21T03:06:57.377Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a106f9baeb644f360', 'GG-T158', 'chore', 'GridGameGaiden', 394287, '2026-10-21T02:16:03.479Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a168ad008e1d0bbd2', 'U.EVE-T20', 'chore', 'GridGameGaiden', 200103, '2026-10-21T02:01:06.541Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a4d63e1c6a967691a', 'UC-T16', 'chore', 'GridGameGaiden', 122543, '2026-10-21T01:50:11.614Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a5ab8b285fac374aa', 'U.EVE-T15', 'chore', 'GridGameGaiden', 31421, '2026-10-21T01:38:33.054Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a5f5da228131771aa', 'UM-T17', 'chore', '(workspace root)', 442755, '2026-10-21T02:47:56.275Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a694ed3406d6cdbc2', 'U.SPC-T317', 'fix', '(workspace root)', 186630, '2026-10-21T02:34:04.641Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a6d053950e81b554f', 'U.EVE-T16', 'chore', '(workspace root)', 334084, '2026-10-21T02:51:20.809Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a7c61807ecfa6a779', 'GG-T80', 'docs', '(workspace root)', 216526, '2026-10-21T04:19:13.485Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a8101f8beb944b357', 'U.SPC-T361', 'chore', '(workspace root)', 1183865, '2026-10-21T02:57:31.461Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-a8bc6920dfb3b543c', 'UM-T35', 'chore', 'GridGameGaiden', 199211, '2026-10-21T01:58:15.205Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-aa83e1bbe25c5dc8d', 'U.SPC-T319', 'fix', 'GridGameGaiden', 107672, '2026-10-21T02:15:45.505Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-ab3cfa10b91b48cd4', 'U.SPC-T315', 'fix', '(workspace root)', 434166, '2026-10-21T02:39:19.187Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-acc0b60034f32dda0', 'UM-T25', 'feature', '(workspace root)', 562751, '2026-10-21T03:11:11.835Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-ad68d021771b08b06', 'U.SPC-T318', 'fix', 'GridGameGaiden', 52058, '2026-10-21T01:51:28.435Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-af670f0876d59303b', 'U.EVE-T14', 'chore', '(workspace root)', 894909, '2026-10-21T02:59:43.422Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('852b98be-444e-4500-b84c-d2f69e073fad/agent-afe149dd0addea669', 'U.EVE-T17', 'docs', '(workspace root)', 445735, '2026-10-21T02:46:36.609Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85360beb-fde9-4ba7-9508-8ad05ee5931d', 'UNI-T39', 'chore', 'Unity.Chain', 16600, '2026-10-24T04:30:27.497Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a11f8b8c0977ee3d5', 'M.CAPT-T15', 'docs', 'MindAttic.Orchestrator', 64456, '2026-10-22T10:03:53.818Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a19ffe7a25e392b77', 'M.CAPT-T12', 'chore', 'MindAttic.Orchestrator', 59422, '2026-10-22T02:24:34.55Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a50cc30cc6ed5092e', 'M.ORCH-T562', 'feature', 'MindAttic.Orchestrator', 70607, '2026-10-21T19:33:57.098Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a6fa60c1f73268435', 'M.CAPT-T46', 'fix', 'MindAttic.Orchestrator', 38956, '2026-10-22T02:42:12.283Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a83ee85abfdfb02e7', 'M.ORCH-T597', 'fix', 'MindAttic.Orchestrator', 14879, '2026-10-21T19:30:37.397Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a8965e6cbf2250baa', 'M.ORCH-T591', 'fix', 'MindAttic.Orchestrator', 69930, '2026-10-21T19:30:01.714Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-a9da66524fcb53098', 'M.CAPT-T12', 'chore', 'MindAttic.Orchestrator', 371809, '2026-10-22T05:47:41.019Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-ab8f378aa8039169c', 'M.ORCH-T600', 'fix', 'MindAttic.Orchestrator', 48913, '2026-10-22T00:43:12.485Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-abe07194939087c4b', 'M.ORCH-T572', 'feature', 'MindAttic.Orchestrator', 837278, '2026-10-22T00:22:04.585Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-ac0bc96e8e442b658', 'M.CAPT-T35', 'docs', 'MindAttic.Orchestrator', 22308, '2026-10-22T02:28:52.902Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-ac3b3433da56f96d7', 'M.ORCH-T599', 'feature', 'MindAttic.Orchestrator', 189319, '2026-10-22T00:09:59.95Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-ad7a84a42500575d8', 'M.CAPT-T14', 'chore', 'MindAttic.Orchestrator', 31684, '2026-10-22T02:39:31.422Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-ae50908581aacce68', 'M.ORCH-T598', 'fix', 'MindAttic.Orchestrator', 48713, '2026-10-21T19:35:53.136Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-ae896777a188d1bde', 'M.CAPT-T13', 'chore', 'MindAttic.Orchestrator', 20271, '2026-10-22T02:30:16.584Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-aeb78a3838879a69f', 'M.CAPT-T12', 'chore', 'MindAttic.Orchestrator', 117975, '2026-10-22T09:48:52.085Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('85827f8f-18e4-4add-b8f6-893ccf7bc987/agent-af02b2c1ccb1d5642', 'M.CAPT-T12', 'chore', 'MindAttic.Orchestrator', 267914, '2026-10-22T08:34:21.678Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8742217d-b340-4e0e-b946-bb395dea8d1f', 'M.ORCH-T774', 'fix', 'MindAttic.Orchestrator', 19021, '2026-10-24T06:08:29.419Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('88f96354-7241-47a3-8646-380771e83f0a', 'SS-T18', 'chore', 'Prose', 166432, '2026-10-15T12:32:34.246Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('89c5e19e-48ce-4f19-bace-d85a156b8b3c', 'M.MDIA-T10', 'docs', 'MindAttic.Media', 15408, '2026-10-21T18:10:33.849Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('89ca541c-cb35-4617-94be-5c87346cb247', 'UR-T60', 'fix', 'Unity.Ricochet', 29035, '2026-10-24T00:11:00.793Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a18e6e5-e63b-4728-9b6b-8f01429d6410', 'M.ORCH-T50', 'feature', 'MindAttic.Orchestrator', 255839, '2026-10-15T07:40:28.95Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a0cf53a0fa405480c', 'U.SPC-T429', 'feature', 'Unity.SpaceGod', 166598, '2026-10-23T14:45:15.471Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a1a4364618b415ca7', 'U.SPC-T428', 'chore', 'Unity.SpaceGod', 188321, '2026-10-22T22:55:51.099Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a1f6073bee8934f81', 'U.SPC-T425', 'feature', 'Unity.SpaceGod', 916810, '2026-10-23T13:33:42.539Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a2224c8606e87003a', 'U.SPC-T426', 'feature', 'Unity.SpaceGod', 698305, '2026-10-23T14:46:17.656Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a31df7cfdaaffea34', 'U.SPC-T424', 'feature', 'Unity.SpaceGod', 437558, '2026-10-23T11:55:11.622Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a38080db0721db452', 'U.SPC-T427', 'feature', 'Unity.SpaceGod', 237550, '2026-10-23T14:45:04.312Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a487b6992075717a9', 'U.SPC-T375', 'feature', 'Unity.SpaceGod', 483029, '2026-10-22T23:29:58.08Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a4f9918ff7d6034af', 'U.SPC-T415', 'feature', 'Unity.SpaceGod', 188552, '2026-10-23T01:27:16.304Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a5c0f6f0726c4732c', 'U.SPC-T325', 'feature', 'Unity.SpaceGod', 149313, '2026-10-22T22:58:17.985Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a5d6a696db1473e56', 'U.SPC-T419', 'feature', 'Unity.SpaceGod', 406660, '2026-10-23T06:19:15.156Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a5e2b08cb0e8f9616', 'U.SPC-T374', 'feature', 'Unity.SpaceGod', 1269950, '2026-10-22T23:48:23.497Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a5f45f3673c8f4421', 'U.SPC-T422', 'feature', 'Unity.SpaceGod', 335010, '2026-10-23T10:44:32.747Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a5f47b1c1f33a6670', 'U.SPC-T379', 'feature', 'Unity.SpaceGod', 252924, '2026-10-23T00:42:10.512Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a65fcfacd816ea3c0', 'U.SPC-T460', 'fix', 'Unity.SpaceGod', 64315, '2026-10-23T14:16:55.173Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a721fb411b701056a', 'U.SPC-T330', 'feature', 'Unity.SpaceGod', 157473, '2026-10-22T23:10:15.358Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a77df51c558ff97d9', 'U.SPC-T453', 'fix', 'Unity.SpaceGod', 2877180, '2026-10-23T05:29:59.615Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a787535a22701716d', 'U.SPC-T423', 'feature', 'Unity.SpaceGod', 358972, '2026-10-23T10:59:27.759Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a7f737088698c24bd', 'U.SPC-T457', 'fix', 'Unity.SpaceGod', 56963, '2026-10-23T08:20:54.94Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a8b43076fb40268ce', 'U.SPC-T431', 'feature', 'Unity.SpaceGod', 546417, '2026-10-22T23:17:49.456Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a90d42cd313883c4e', 'U.SPC-T377', 'feature', 'Unity.SpaceGod', 378080, '2026-10-23T00:23:49.671Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a92f2f483e6333eb2', 'U.SPC-T458', 'fix', 'Unity.SpaceGod', 57565, '2026-10-23T13:57:35.891Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-a97b51289fbe1fbca', 'U.SPC-T417', 'feature', 'Unity.SpaceGod', 13903099, '2026-10-23T14:45:42.169Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-aad33a26fca5c9847', 'U.SPC-T412', 'feature', 'Unity.SpaceGod', 1003418, '2026-10-23T01:04:10.589Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-aafac5547536b4181', 'U.SPC-T376', 'feature', 'Unity.SpaceGod', 163212, '2026-10-22T23:58:46.008Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-aafed4a3954f0c3dc', 'U.SPC-T420', 'feature', 'Unity.SpaceGod', 398185, '2026-10-23T07:34:06.899Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-ac9b7327a31bac1c1', 'U.SPC-T413', 'feature', 'Unity.SpaceGod', 287013, '2026-10-23T01:37:24.997Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-ace1aa44365680043', 'U.SPC-T421', 'feature', 'Unity.SpaceGod', 337334, '2026-10-23T09:55:50.804Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-ad53e2cccdd8dcdea', 'U.SPC-T418', 'feature', 'Unity.SpaceGod', 456622, '2026-10-23T06:10:45.733Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-ae2a5971bf729b3ec', 'U.SPC-T459', 'fix', 'Unity.SpaceGod', 66462, '2026-10-23T14:07:47.197Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8a8e89ea-45f7-4548-95f2-ba1c125840d7/agent-ae4b24acc323cccdc', 'U.SPC-T414', 'feature', 'Unity.SpaceGod', 277663, '2026-10-23T01:15:33.77Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8abd01b3-84d2-46c9-a9a7-6b425770e123', 'U.RON-T11', 'docs', 'UnityRonin', 15219, '2026-10-21T18:10:48.288Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8c775618-bedb-40ce-a12d-9d5991cf8791', 'USS-T18', 'feature', 'UnitySolarSystem', 111374, '2026-10-15T10:34:39.901Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8cfb5fdd-a868-49e0-bb37-fb493a29aefa/agent-a2d208eafa7d30ec7', 'UQD-T10', 'docs', 'Unity.QuickDuel', 98085, '2026-10-22T07:47:26.685Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8cfb5fdd-a868-49e0-bb37-fb493a29aefa/agent-a40bfa1a3186a2e69', 'UQD-T17', 'fix', 'Unity.QuickDuel', 40834, '2026-10-22T09:01:55.927Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9066c252-4e61-4adc-8e1b-61120f513162', 'SS-T56', 'chore', 'Prose', 309300, '2026-10-15T13:05:39.825Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9173831a-1a8b-45e5-ab65-3105b64e8e81', 'GG-T28', 'chore', 'GridGameGaiden', 96220, '2026-10-15T11:11:55.835Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a1dcdb938a4ee3a7e', 'UNI-T50', 'feature', '(workspace root)', 246921, '2026-10-24T03:59:53.952Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a1ff714e93f35487a', 'U.INC-T21', 'chore', '(workspace root)', 220020, '2026-10-24T10:41:57.145Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a22dc7b8cca4c5148', 'UNI-T38', 'chore', '(workspace root)', 396864, '2026-10-24T04:13:34.64Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a435fa3901db1d73d', 'UR-T36', 'chore', '(workspace root)', 221313, '2026-10-24T11:18:02.044Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a43a6f6b8a3a3ffba', 'UH-T40', 'chore', '(workspace root)', 272255, '2026-10-24T10:52:17.455Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a552278f9f9ec8379', 'UNI-T52', 'feature', '(workspace root)', 223789, '2026-10-24T10:31:18.411Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a5769b3903ee76f14', 'UNI-T51', 'feature', '(workspace root)', 273675, '2026-10-24T04:44:15.133Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a6c75354adb597032', 'UR-T68', 'chore', '(workspace root)', 154968, '2026-10-24T10:52:07.208Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a8714bba7c371729b', 'UNI-T39', 'chore', '(workspace root)', 247866, '2026-10-24T04:33:41.078Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a89130feffb168025', 'UNI-T55', 'feature', '(workspace root)', 246676, '2026-10-24T11:17:49.404Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a9b2393e0ef3ab9b9', 'UNI-T49', 'feature', '(workspace root)', 111040, '2026-10-24T03:48:42.322Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a9b41ad36233c2280', 'UNI-T37', 'feature', '(workspace root)', 82412, '2026-10-24T03:44:25.473Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-a9db29988c955c838', 'U.VAN-T6', 'chore', '(workspace root)', 178439, '2026-10-24T10:40:13.957Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-ab43e84031f007a6e', 'U.OVW-T6', 'chore', '(workspace root)', 144004, '2026-10-24T10:36:21.772Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-ab5512d8ba1ba2433', 'UH-T41', 'chore', '(workspace root)', 412335, '2026-10-24T11:18:05.647Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-abcf2ca39a30f3afe', 'UNI-T54', 'feature', '(workspace root)', 122166, '2026-10-24T11:07:16.948Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-acea623bacf84d77e', 'UQD-T57', 'fix', '(workspace root)', 333685, '2026-10-24T10:48:27.227Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-ad689c1126b135734', 'UNI-T48', 'feature', '(workspace root)', 55396, '2026-10-24T03:43:28.712Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('920f73e2-defc-4a55-b788-dcf01eab5395/agent-afd575f88843491ae', 'UNI-T53', 'feature', '(workspace root)', 115911, '2026-10-24T10:36:26.647Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('921f5247-7f20-4700-9252-adce6094b88e', 'RB-T100', 'fix', 'REA.Bloodlines', 69196, '2026-10-23T05:31:31.256Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('92a0e15b-cf59-4e2b-b26d-883038beeda1', 'U.INC-T26', 'chore', 'Unity.Incognito', 176279, '2026-10-24T14:15:53.945Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('933b404e-17e7-4c76-9234-ad8509611d86', 'GG-T29', 'chore', 'GridGameGaiden', 50328, '2026-10-15T11:18:54.85Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('934a0da6-dfdb-47b7-8c59-5df1ed5b172b', 'SS-T60', 'chore', 'Prose', 296593, '2026-10-15T18:09:02.789Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('93b81c61-f9fc-41d4-ab9f-1a8c155db40a', 'RB-T119', 'docs', 'REA.Bloodlines', 16298, '2026-10-22T20:18:00.157Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('94fd2733-b325-443d-adfc-d8c7ef9f5bdd/agent-a90a83e014f6b6138', 'U.OVW-T38', 'feature', 'Unity.Overworld', 281256, '2026-10-24T11:31:58.684Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9510051b-bedf-48e0-ae56-8e23c87f025a', 'GG-T24', 'chore', 'GridGameGaiden', 33700, '2026-10-15T11:17:14.741Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('96541e7d-6fb6-4840-8461-38447bb71a72', 'IP-T22', 'docs', 'IdiotProof', 18393, '2026-10-21T18:11:49.252Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('96a6d6c3-6ddd-416b-9ef7-b549d23e1749', 'M.VALT-T13', 'docs', 'MindAttic.Vault', 18830, '2026-10-21T18:11:03.274Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a0417ae7865716731', 'M.ORCH-T197', 'fix', 'MindAttic.Orchestrator', 64106, '2026-10-17T06:14:42.115Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a06bab6a91dea397c', 'M.ORCH-T141', 'chore', 'MindAttic.Orchestrator', 49474, '2026-10-16T23:31:41.076Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a08a41884cab08542', 'M.ORCH-T138', 'chore', 'MindAttic.Orchestrator', 49846, '2026-10-16T23:22:13.781Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a0baaf30507b0bced', 'M.ORCH-T139', 'chore', 'MindAttic.Orchestrator', 81705, '2026-10-16T23:35:52.843Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a20862cdc043b49cb', 'M.ORCH-T142', 'chore', 'MindAttic.Orchestrator', 64921, '2026-10-16T23:35:29.625Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a4ad3fcc75b3811bf', 'M.ORCH-T136', 'chore', 'MindAttic.Orchestrator', 64849, '2026-10-16T23:16:25.57Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a639fe4283ef0434e', 'M.ORCH-T149', 'chore', 'MindAttic.Orchestrator', 86340, '2026-10-17T06:25:42.758Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a64eb29f5c2e0e88d', 'M.ORCH-T144', 'chore', 'MindAttic.Orchestrator', 52587, '2026-10-16T23:39:29.746Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a68bf9cc4d2df9353', 'M.ORCH-T143', 'chore', 'MindAttic.Orchestrator', 53137, '2026-10-16T23:31:56.71Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a7de5ecb7a7a45fa3', 'M.ORCH-T140', 'chore', 'MindAttic.Orchestrator', 64132, '2026-10-16T23:26:59.804Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a85c2069e907b2970', 'M.ORCH-T147', 'chore', 'MindAttic.Orchestrator', 61247, '2026-10-17T06:23:10.975Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a9589336f482fddbc', 'M.ORCH-T193', 'feature', 'MindAttic.Orchestrator', 47914, '2026-10-17T01:29:42.013Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-a985bf105c8c4461c', 'M.ORCH-T192', 'feature', 'MindAttic.Orchestrator', 109041, '2026-10-17T02:10:29.649Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-aa99613b0518466a1', 'M.ORCH-T145', 'chore', 'MindAttic.Orchestrator', 44162, '2026-10-17T01:41:49.368Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-aacde50a5b3f401e7', 'M.ORCH-T195', 'fix', 'MindAttic.Orchestrator', 63799, '2026-10-17T06:11:23.855Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ab78bb9ac57a5254b', 'M.ORCH-T191', 'feature', 'MindAttic.Orchestrator', 58239, '2026-10-17T01:36:23.028Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-aba8977041db59693', 'M.ORCH-T134', 'chore', 'MindAttic.Orchestrator', 55061, '2026-10-16T22:40:48.946Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-abdc50678cb7e2874', 'M.ORCH-T150', 'chore', 'MindAttic.Orchestrator', 63534, '2026-10-17T06:23:31.636Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-acc2c3568d229e82d', 'M.ORCH-T137', 'chore', 'MindAttic.Orchestrator', 27467, '2026-10-16T23:13:52.828Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-acf372ab95018b87e', 'M.ORCH-T148', 'chore', 'MindAttic.Orchestrator', 68440, '2026-10-17T06:15:48.899Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ad03d8f347a487b3d', 'M.ORCH-T146', 'chore', 'MindAttic.Orchestrator', 39485, '2026-10-17T06:06:04.882Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ad8bb722efe86c1d9', 'M.ORCH-T135', 'chore', 'MindAttic.Orchestrator', 87137, '2026-10-16T22:53:31.144Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9774783d-f5be-4f4f-8887-72faf291cdb9/agent-ae1255311c006be29', 'M.ORCH-T133', 'chore', 'MindAttic.Orchestrator', 86968, '2026-10-16T22:36:12.796Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a044516ea2ad07f1c', 'M.ORCH-T729', 'fix', 'MindAttic.Orchestrator', 36870, '2026-10-23T20:22:52.618Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a04cb3b47e34073b6', 'M.ORCH-T715', 'fix', 'MindAttic.Orchestrator', 35931, '2026-10-23T20:15:30.725Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a0cf49ea2c3c8ae4a', 'M.ORCH-T738', 'fix', 'MindAttic.Orchestrator', 35184, '2026-10-23T20:32:50.078Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a0e2ff136f8337d72', 'M.ORCH-T750', 'fix', 'MindAttic.Orchestrator', 48142, '2026-10-23T22:52:01.659Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a11147fb49a38b765', 'M.ORCH-T707', 'fix', 'MindAttic.Orchestrator', 386083, '2026-10-23T20:31:28.239Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a1a97af7376235a6b', 'M.ORCH-T735', 'fix', 'MindAttic.Orchestrator', 26318, '2026-10-23T20:23:32.241Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a1acb684da18d9024', 'M.ORCH-T711', 'fix', 'MindAttic.Orchestrator', 48428, '2026-10-23T20:13:03.442Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a1e7323e6ffc2b264', 'M.ORCH-T714', 'fix', 'MindAttic.Orchestrator', 41919, '2026-10-23T20:16:31.028Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a21ac5c007830c3af', 'M.ORCH-T717', 'fix', 'MindAttic.Orchestrator', 40376, '2026-10-23T20:17:59.078Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a2480600c4e186abe', 'M.ORCH-T753', 'feature', 'MindAttic.Orchestrator', 251129, '2026-10-24T00:25:37.691Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a28a8ad20bba01a7a', 'M.TIX-T119', 'docs', 'MindAttic.Orchestrator', 31496, '2026-10-24T01:09:50.017Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a28ff7094e5d91c98', 'M.ORCH-T763', 'feature', 'MindAttic.Orchestrator', 491691, '2026-10-24T01:57:50.76Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a2fe9de21c38635f9', 'M.ORCH-T761', 'fix', 'MindAttic.Orchestrator', 294181, '2026-10-24T01:21:01.443Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a328aa09ebb965c9a', 'M.ORCH-T713', 'fix', 'MindAttic.Orchestrator', 28256, '2026-10-23T20:10:18.739Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a367ec2aef8d33434', 'M.ORCH-T727', 'fix', 'MindAttic.Orchestrator', 52537, '2026-10-23T20:21:17.963Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a37c32ace3ea87a8d', 'M.ORCH-T730', 'fix', 'MindAttic.Orchestrator', 34745, '2026-10-23T20:29:56.834Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a3ff8f7989a64dc26', 'M.ORCH-T741', 'fix', 'MindAttic.Orchestrator', 67743, '2026-10-23T20:33:02.628Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a431113a9a824789c', 'M.ORCH-T744', 'fix', 'MindAttic.Orchestrator', 33801, '2026-10-23T20:40:41.11Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a43db57f247c58c34', 'M.ORCH-T703', 'fix', 'MindAttic.Orchestrator', 50609, '2026-10-23T19:51:36.463Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a4476f242310fe964', 'M.ORCH-T723', 'fix', 'MindAttic.Orchestrator', 22951, '2026-10-23T20:18:22.695Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a46f011543c6de243', 'M.ORCH-T743', 'fix', 'MindAttic.Orchestrator', 119145, '2026-10-23T20:45:02.127Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a4969917ed7e52671', 'M.TIX-T131', 'feature', 'MindAttic.Orchestrator', 31056, '2026-10-24T01:07:13.872Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a54102bf0a3e1fc6b', 'M.ORCH-T752', 'feature', 'MindAttic.Orchestrator', 64817, '2026-10-23T22:58:58.894Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a5a916272577d13e8', 'M.ORCH-T726', 'fix', 'MindAttic.Orchestrator', 33697, '2026-10-23T20:20:42.528Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a5b42ce23592932eb', 'M.TIX-T134', 'feature', 'MindAttic.Orchestrator', 304960, '2026-10-24T01:20:32.84Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a5de523e2b7f69e8d', 'M.ORCH-T754', 'feature', 'MindAttic.Orchestrator', 246279, '2026-10-24T00:22:07.813Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a6115c03b29863901', 'M.ORCH-T740', 'fix', 'MindAttic.Orchestrator', 46536, '2026-10-23T20:29:16.739Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a635ba9fa886252fe', 'M.ORCH-T762', 'feature', 'MindAttic.Orchestrator', 284844, '2026-10-24T01:57:59.288Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a67727f954b21a5ba', 'M.ORCH-T710', 'fix', 'MindAttic.Orchestrator', 51088, '2026-10-23T20:16:19.216Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a67937ef8dc743109', 'M.ORCH-T705', 'fix', 'MindAttic.Orchestrator', 49180, '2026-10-23T20:02:48.383Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a68451d167b97a24e', 'M.ORCH-T765', 'fix', 'MindAttic.Orchestrator', 63962, '2026-10-24T01:40:20.956Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a74a5271b8373a522', 'M.ORCH-T736', 'fix', 'MindAttic.Orchestrator', 31265, '2026-10-23T20:25:26.683Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a755a4a3dc051b677', 'M.TIX-T133', 'feature', 'MindAttic.Orchestrator', 121977, '2026-10-24T01:11:20.756Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a7b864d1206386388', 'M.ORCH-T722', 'fix', 'MindAttic.Orchestrator', 37172, '2026-10-23T20:19:37.988Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a7c780e310a477ecb', 'M.ORCH-T746', 'fix', 'MindAttic.Orchestrator', 56861, '2026-10-23T21:03:45.669Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a7d2a61afdc9af9c0', 'M.ORCH-T716', 'fix', 'MindAttic.Orchestrator', 41004, '2026-10-23T20:17:59.73Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a7dad48d99200c164', 'M.TIX-T138', 'feature', 'MindAttic.Orchestrator', 202289, '2026-10-24T01:42:19.742Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a82e5376d62faa712', 'M.ORCH-T745', 'fix', 'MindAttic.Orchestrator', 65495, '2026-10-23T20:47:37.778Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a93848b9352015074', 'M.ORCH-T747', 'fix', 'MindAttic.Orchestrator', 104382, '2026-10-23T22:28:44.437Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a94ea01c4443f121d', 'M.ORCH-T704', 'fix', 'MindAttic.Orchestrator', 30329, '2026-10-23T19:55:45.629Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a96af401fd6782e8c', 'M.ORCH-T712', 'fix', 'MindAttic.Orchestrator', 40819, '2026-10-23T20:10:37.006Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a996eaeddd3ee70a4', 'M.TIX-T137', 'feature', 'MindAttic.Orchestrator', 701561, '2026-10-24T01:50:28.466Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a9e000137e9425859', 'M.ORCH-T720', 'fix', 'MindAttic.Orchestrator', 80890, '2026-10-23T20:25:29.662Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-a9e1948702ea69199', 'M.ORCH-T733', 'fix', 'MindAttic.Orchestrator', 26491, '2026-10-23T20:21:59.201Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aa234395598349089', 'M.ORCH-T709', 'fix', 'MindAttic.Orchestrator', 28225, '2026-10-23T20:09:38.431Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aa4c84175a9083bfc', 'M.ORCH-T737', 'fix', 'MindAttic.Orchestrator', 28318, '2026-10-23T20:26:27.665Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aa6097d5afa335756', 'M.ORCH-T725', 'fix', 'MindAttic.Orchestrator', 51695, '2026-10-23T20:27:03.495Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aac0f378a118f9207', 'M.ORCH-T742', 'fix', 'MindAttic.Orchestrator', 91750, '2026-10-23T20:38:56.012Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ab021c0c6c7f21737', 'M.ORCH-T731', 'fix', 'MindAttic.Orchestrator', 46034, '2026-10-23T20:23:43.813Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ab590badcb4ea0082', 'M.ORCH-T706', 'fix', 'MindAttic.Orchestrator', 270944, '2026-10-23T20:27:46.12Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ab9c5033da00b048d', 'M.ORCH-T724', 'fix', 'MindAttic.Orchestrator', 24801, '2026-10-23T20:18:29.587Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-abd8deeba9ae88db0', 'M.ORCH-T699', 'feature', 'MindAttic.Orchestrator', 116000, '2026-10-23T19:47:54.971Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ac15180ae2c4cc2f3', 'M.ORCH-T751', 'feature', 'MindAttic.Orchestrator', 312434, '2026-10-23T23:05:46.61Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ac244ae8981388032', 'M.ORCH-T728', 'fix', 'MindAttic.Orchestrator', 61375, '2026-10-23T20:24:16.116Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aca9c7c993c64bb2e', 'M.ORCH-T767', 'feature', 'MindAttic.Orchestrator', 114811, '2026-10-24T01:44:04.887Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ace4b0b8d8c2d488b', 'M.ORCH-T719', 'fix', 'MindAttic.Orchestrator', 29606, '2026-10-23T20:18:06.45Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ace77b6fcbbc97260', 'M.ORCH-T734', 'fix', 'MindAttic.Orchestrator', 39068, '2026-10-23T20:24:45.083Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-acf4e6aaa2e88946d', 'M.ORCH-T766', 'feature', 'MindAttic.Orchestrator', 197812, '2026-10-24T01:49:15.476Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ad8f6e25cfa44ca41', 'M.ORCH-T748', 'feature', 'MindAttic.Orchestrator', 282470, '2026-10-23T22:49:26.309Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-adabaa4fd2e851c82', 'M.TIX-T135', 'feature', 'MindAttic.Orchestrator', 233549, '2026-10-24T01:35:05.241Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-add626500a943c349', 'M.ORCH-T732', 'fix', 'MindAttic.Orchestrator', 51459, '2026-10-23T20:27:53.094Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ae198b8434e5030a2', 'M.ORCH-T700', 'feature', 'MindAttic.Orchestrator', 79504, '2026-10-23T19:47:32.686Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ae518296e102a9e86', 'M.ORCH-T721', 'fix', 'MindAttic.Orchestrator', 49256, '2026-10-23T20:19:22.861Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-ae6a6a5e819e3ac54', 'M.ORCH-T755', 'feature', 'MindAttic.Orchestrator', 146903, '2026-10-24T00:15:22.537Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aeb761b9ed4e0cc05', 'M.ORCH-T702', 'feature', 'MindAttic.Orchestrator', 184628, '2026-10-23T19:51:01.329Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-aeba06d798ad5cdef', 'M.ORCH-T718', 'fix', 'MindAttic.Orchestrator', 37903, '2026-10-23T20:17:47.354Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-af20b4da64eed633a', 'M.ORCH-T757', 'feature', 'MindAttic.Orchestrator', 200654, '2026-10-24T01:15:07.636Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-af65b2f5b4b31e73c', 'M.ORCH-T756', 'fix', 'MindAttic.Orchestrator', 55689, '2026-10-23T23:34:53.81Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-af6fd0ce65825cf83', 'M.ORCH-T655', 'feature', 'MindAttic.Orchestrator', 199137, '2026-10-23T23:18:54.152Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-af74c9e25d5a06f91', 'M.ORCH-T696', 'feature', 'MindAttic.Orchestrator', 196359, '2026-10-23T19:53:59.883Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-af858d648592e40c5', 'M.ORCH-T749', 'fix', 'MindAttic.Orchestrator', 82525, '2026-10-23T22:33:32.83Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-af86d07e72d8d0ad5', 'M.ORCH-T708', 'fix', 'MindAttic.Orchestrator', 37679, '2026-10-23T20:11:09.559Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('98a40e2a-cc75-4838-86e0-d73a698227b1/agent-afb5506c170ca0700', 'M.ORCH-T701', 'feature', 'MindAttic.Orchestrator', 124387, '2026-10-23T19:50:01.362Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-a0af78a7dcf232d26', 'M.ORCH-T779', 'chore', 'MindAttic.Orchestrator', 514626, '2026-10-24T06:17:12.469Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-a0b263afde1fde628', 'M.ORCH-T780', 'fix', 'MindAttic.Orchestrator', 29951, '2026-10-24T05:50:17.117Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-a1bed37b18c72b795', 'M.ORCH-T775', 'fix', 'MindAttic.Orchestrator', 329999, '2026-10-24T05:40:02.485Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-a29e00add8f985de1', 'M.ORCH-T776', 'fix', 'MindAttic.Orchestrator', 205474, '2026-10-24T05:37:08.78Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-ab10865b9cdc2c8b8', 'M.ORCH-T777', 'fix', 'MindAttic.Orchestrator', 165995, '2026-10-24T05:33:47.843Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-af39044e6335f4464', 'M.ORCH-T781', 'fix', 'MindAttic.Orchestrator', 43661, '2026-10-24T06:40:01.295Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9976eacd-149e-4ad5-a16b-b31ecc8eb4cc/agent-af714b0fbcd7de029', 'M.ORCH-T774', 'fix', 'MindAttic.Orchestrator', 2353327, '2026-10-24T06:35:15.126Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a1ad58b-3d01-4256-8b5d-198ddfc7c911', 'M.LOGS-T12', 'docs', 'MindAttic.Log', 15478, '2026-10-21T18:10:56.224Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a45ef64-29c3-43a3-95b0-479a222265c7', 'U.INC-T22', 'chore', 'Unity.Incognito', 60457, '2026-10-24T12:03:24.529Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a851808-0c2d-48ed-8e81-75b916af6fb1', 'SS-T22', 'chore', 'Prose', 36102, '2026-10-15T11:56:51.712Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9bd28781-420c-4c16-b9bf-b4b7b470ed37', 'U.SPC-T46', 'feature', 'UnitySolarSystem', 191690, '2026-10-15T11:19:49.684Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9c7e60f7-f4ef-4140-92df-169c5db99385', 'UQD-T30', 'chore', 'Unity.QuickDuel', 20668, '2026-10-24T12:23:49.441Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9cadf186-6367-4156-bc32-0c0fd9c58358', 'UR-T35', 'chore', 'Unity.Ricochet', 100895, '2026-10-24T01:11:31.266Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9d42b472-3333-40e8-8d9f-d137053afb51', 'SS-T35', 'chore', 'Prose', 28013, '2026-10-15T11:58:00.618Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9e0a72f4-6204-4817-8055-4b4a3f8119ef', 'M.ORCH-T737', 'fix', 'MindAttic.Orchestrator', 3816, '2026-10-23T20:24:54.366Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9eb76350-c323-4ffc-ab29-14cdc310dab9', 'GAD-T2', 'chore', 'Gadfly', 37325, '2026-10-16T00:54:09.924Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a220ff0fbfe8d8ea6', 'U.VAN-T24', 'fix', 'Unity.ProjectVania', 221728, '2026-10-24T12:46:00.99Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a579233a63ba1539f', 'U.VAN-T16', 'feature', 'Unity.ProjectVania', 189178, '2026-10-24T15:21:48.618Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a765c7b7f5ecc2437', 'U.VAN-T15', 'feature', 'Unity.ProjectVania', 349661, '2026-10-24T14:59:38.339Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a7a8b3d1fad51bd21', 'U.VAN-T8', 'feature', 'Unity.ProjectVania', 113732, '2026-10-24T11:42:16.912Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a7c7c9ef50cf6010a', 'U.VAN-T17', 'feature', 'Unity.ProjectVania', 443996, '2026-10-24T15:49:50.913Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a8f178e086c4789d8', 'U.VAN-T7', 'feature', 'Unity.ProjectVania', 281043, '2026-10-24T11:33:46.988Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a8fa41da34e5504b7', 'U.VAN-T9', 'feature', 'Unity.ProjectVania', 244355, '2026-10-24T11:58:57.695Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-a952ddaf5dff2ee07', 'U.VAN-T10', 'feature', 'Unity.ProjectVania', 273866, '2026-10-24T13:02:14.605Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-ac1131348bf1d3fb9', 'U.VAN-T11', 'feature', 'Unity.ProjectVania', 219654, '2026-10-24T13:21:18.598Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-ac48893e3c2d8d192', 'U.VAN-T14', 'feature', 'Unity.ProjectVania', 340330, '2026-10-24T14:26:46.602Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-acd6eceadb59ee5da', 'U.VAN-T12', 'feature', 'Unity.ProjectVania', 249830, '2026-10-24T13:37:02.311Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-af130cfd59eac5c1d', 'U.VAN-T13', 'feature', 'Unity.ProjectVania', 324996, '2026-10-24T13:56:44.493Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f1c1214-cf01-4b91-9203-0ab313f699ee/agent-af7be1c28ee09ac72', 'U.VAN-T23', 'fix', 'Unity.ProjectVania', 185954, '2026-10-24T12:24:06.551Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9f20f70a-158d-4a63-a196-bf7551e7790e', 'M.ORCH-T88', 'feature', 'MindAttic.Orchestrator', 102698, '2026-10-15T08:28:26.22Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9fe45815-a62c-4adf-9387-4012a7bbbc50', 'GG-T25', 'chore', 'GridGameGaiden', 85779, '2026-10-15T11:17:20.874Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a0117a5f-7f1b-4b57-bb34-f1802ac5acdb', 'SS-T26', 'chore', 'Prose', 229926, '2026-10-15T12:43:15.261Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a08981a5-ebf6-4047-8e82-65210ac55dc6', 'GG-T30', 'chore', 'GridGameGaiden', 139899, '2026-10-16T01:44:08.446Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a161be04-0cf5-4976-a312-431ff54cfc4d', 'GG-T28', 'chore', 'GridGameGaiden', 116043, '2026-10-16T01:47:39.865Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a3a3a74a-e986-4c10-ab99-820001ddf0a3', 'M.DPLY-T3', 'chore', 'MindAttic.Deploy', 94337, '2026-10-16T01:53:16.766Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a0fea105d6b586e9b', 'U.SPC-T307', 'fix', 'UnitySpace', 44775, '2026-10-20T21:54:08.07Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a369a8fe23f6cf85c', 'U.SPC-T310', 'fix', 'UnitySpace', 517071, '2026-10-20T23:02:20.22Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a4e7a6ea4dab154df', 'U.SPC-T308', 'fix', 'UnitySpace', 160378, '2026-10-20T22:27:09.036Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a4f11e367409118bc', 'U.SPC-T321', 'fix', 'UnitySpace', 274608, '2026-10-20T22:41:44.694Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a620689ad861a2be3', 'U.SPC-T309', 'fix', 'UnitySpace', 183590, '2026-10-20T22:38:08.072Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a7bf7abcd6c5a424a', 'U.SPC-T306', 'fix', 'UnitySpace', 51656, '2026-10-20T21:56:05.572Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-a9f479731441f8485', 'U.SPC-T312', 'fix', 'UnitySpace', 207203, '2026-10-20T22:24:24.729Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-aa203cc7f022350ba', 'U.SPC-T313', 'fix', 'UnitySpace', 226768, '2026-10-20T22:37:28.764Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-aacef402c12e66b0e', 'U.SPC-T311', 'fix', 'UnitySpace', 258883, '2026-10-20T22:40:13.268Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-abe50d9947774f852', 'U.SPC-T314', 'fix', 'UnitySpace', 180956, '2026-10-20T23:00:19.281Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-ac63f95d0583a51a6', 'U.SPC-T303', 'fix', 'UnitySpace', 45798, '2026-10-20T21:54:30.235Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-ac6a8066653293ca9', 'U.SPC-T49', 'chore', 'UnitySpace', 1644302, '2026-10-21T00:29:50.956Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-acd0b5cd00a73274a', 'U.SPC-T302', 'fix', 'UnitySpace', 224914, '2026-10-20T22:49:08.326Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-acede121a2caf942c', 'U.SPC-T304', 'fix', 'UnitySpace', 446433, '2026-10-20T22:51:09.959Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a4a33f7b-c63b-4b78-a58b-ac731eded8b0/agent-aed48f8824ac2fb56', 'U.SPC-T301', 'fix', 'UnitySpace', 67783, '2026-10-20T21:52:59.211Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a07467bf8de6f1997', 'M.ORCH-T477', 'feature', 'MindAttic.Orchestrator', 900947, '2026-10-19T23:13:06.761Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a08f4e96b74685759', 'M.ORCH-T486', 'feature', 'MindAttic.Orchestrator', 568009, '2026-10-19T23:48:21.335Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a19f8d17e2fafb3e4', 'M.ORCH-T481', 'feature', 'MindAttic.Orchestrator', 1343192, '2026-10-19T23:11:35.372Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a45c098a04674ef41', 'M.ORCH-T475', 'chore', 'MindAttic.Orchestrator', 178449, '2026-10-19T22:41:31.852Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a699a227d34ee6ebc', 'M.ORCH-T482', 'fix', 'MindAttic.Orchestrator', 48128, '2026-10-19T22:34:37.195Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-a7ee4a41ea30883f6', 'M.ORCH-T483', 'feature', 'MindAttic.Orchestrator', 266027, '2026-10-19T22:43:46.27Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-aa266a8ff05f8d788', 'M.ORCH-T485', 'fix', 'MindAttic.Orchestrator', 245844, '2026-10-19T22:50:52.675Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-ab41e989c253aafe0', 'M.ORCH-T468', 'fix', 'MindAttic.Orchestrator', 29978, '2026-10-19T22:35:24.678Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-ab7eccfef6edeb7c3', 'M.ORCH-T489', 'fix', 'MindAttic.Orchestrator', 240876, '2026-10-19T23:22:01.348Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-af274d2704d8ef5f4', 'M.ORCH-T490', 'feature', 'MindAttic.Orchestrator', 581844, '2026-10-20T01:34:30.336Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a65aead5-56bb-429b-898d-67cb68fa5b27/agent-afa4414d300ad131f', 'M.ORCH-T479', 'fix', 'MindAttic.Orchestrator', 315599, '2026-10-19T22:44:32.323Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a697ee44-c933-4ec2-97b6-ce3724647c0c', 'M.HELP-T7', 'docs', 'MindAttic.Helpers', 17551, '2026-10-21T18:10:28.109Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a76cc142-503d-4beb-840e-d42cb977fb89', 'M.DPLY-T12', 'docs', 'MindAttic.Deploy', 18782, '2026-10-21T18:10:50.837Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a860eb4f-87f7-48ca-b32d-5a51b75d369e', 'USS-T17', 'feature', 'UnitySolarSystem', 138831, '2026-10-15T10:27:07Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a988c566-86ca-491b-9553-adf19cca46c6/agent-a65109e71314b6769', 'GG-T175', 'feature', 'Unity.GridGame', 927199, '2026-10-22T09:52:59.139Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a988c566-86ca-491b-9553-adf19cca46c6/agent-ad574cab5b496ca98', 'GG-T177', 'feature', 'Unity.GridGame', 707434, '2026-10-22T11:21:55.879Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a988c566-86ca-491b-9553-adf19cca46c6/agent-afb7a058ffe6ba8f9', 'GG-T176', 'feature', 'Unity.GridGame', 486179, '2026-10-22T10:31:19.595Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa51e672-2cba-4722-a561-d57d0bc86968', 'UR-T33', 'chore', 'Unity.Ricochet', 55251, '2026-10-23T22:53:43.356Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a074538cdff78e942', 'M.WD-T2', 'feature', 'MindAttic.WatchDog', 66577, '2026-10-19T02:08:48.926Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a16cda2cb1ea50393', 'M.WD-T3', 'chore', 'MindAttic.WatchDog', 68603, '2026-10-19T02:08:10.077Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a199b1e5a48505a2f', 'M.WD-T6', 'feature', 'MindAttic.WatchDog', 34539, '2026-10-19T15:16:45.02Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a434370eb192fa4cf', 'M.WD-T4', 'feature', 'MindAttic.WatchDog', 49580, '2026-10-19T02:38:08.537Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a59a09d3ce301a960', 'M.WD-T8', 'feature', 'MindAttic.WatchDog', 52807, '2026-10-19T15:20:18.951Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-a9b005b09f85def97', 'M.WD-T9', 'feature', 'MindAttic.WatchDog', 44021, '2026-10-19T15:28:46.753Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-ab44faf67d61420a8', 'M.WD-T5', 'feature', 'MindAttic.WatchDog', 99563, '2026-10-19T15:26:43.45Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-ac5677315729a0c5b', 'M.WD-T7', 'feature', 'MindAttic.WatchDog', 59775, '2026-10-19T15:24:02.337Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-acd6527bdf8072ee6', 'M.WD-T11', 'feature', 'MindAttic.WatchDog', 55854, '2026-10-19T15:33:10.397Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa65d761-0831-44de-b5c6-c7ccc143108a/agent-adf8a533c97c1713f', 'MAT-T4', 'docs', 'MindAttic.WatchDog', 41165, '2026-10-19T02:32:38.655Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('aa706789-f355-4bd6-9a29-cc6d8a53c560', 'AUT-T4', 'chore', 'Automata', 135488, '2026-10-16T00:25:47.242Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('abb70b4a-9c75-4b82-b7db-cf800c20851c', 'UR-T24', 'fix', 'Unity.Ricochet', 21556, '2026-10-23T22:18:09.822Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('abf80f04-4fe0-4b04-877d-2ccb1ccb214d', 'U.VAN-T2', 'chore', 'Unity.ProjectVania', 88820, '2026-10-23T22:23:25.349Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ac01f4f8-fd13-438f-ba25-1c90451e79d9', 'GG-T47', 'feature', 'GridGameGaiden', 336822, '2026-10-15T15:49:46.277Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ad10f6e2-debc-48b7-831a-4930f19d2f33', 'M.LEGN-T3', 'chore', 'MindAttic.Legion', 64505, '2026-10-16T01:52:34.187Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ad684285-6ecc-4089-84b3-ca0a3c442f97', 'U.SPC-T389', 'fix', 'Unity.SpaceGod', 72391, '2026-10-22T02:29:18.798Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ad7fbc23-533a-485c-adef-befaa1d47626', 'M.LNCH-T14', 'chore', 'MindAttic.Launcher', 15312, '2026-10-23T19:17:27.187Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a11f2377b4047bb51', 'G.AWPN-T180', 'fix', 'GTFO.EZ', 136891, '2026-10-20T01:04:37.965Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a20be145d2b240cfc', 'G.AWPN-T176', 'fix', 'GTFO.EZ', 60398, '2026-10-20T00:54:33.81Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a43ff014bd35a22fa', 'G.EZ-T87', 'fix', 'GTFO.EZ', 48087, '2026-10-19T22:37:00.868Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a4cde1d04d7e374e2', 'G.EZ-T10', 'feature', 'GTFO.EZ', 36953, '2026-10-20T00:52:37.285Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a54a82f9d11c041cb', 'GAM-T42', 'fix', 'GTFO.EZ', 29422, '2026-10-20T00:53:35.432Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a57123e7adacb89ea', 'GAM-T38', 'docs', 'GTFO.EZ', 26154, '2026-10-20T00:53:01.461Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a62eb4ccb7cd966c9', 'G.AWPN-T177', 'fix', 'GTFO.EZ', 33649, '2026-10-20T00:53:33.565Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a73afae4faa18fa35', 'G.AWPN-T178', 'fix', 'GTFO.EZ', 38749, '2026-10-20T00:53:58.532Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-a864b77575f480648', 'G.AWPN-T11', 'docs', 'GTFO.EZ', 188657, '2026-10-20T01:08:52.746Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-ae342d9d9099ea5fc', 'G.AWPN-T179', 'docs', 'GTFO.EZ', 85781, '2026-10-20T00:55:53.323Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-ae7cb7ce9c4062654', 'G.EZ-T89', 'fix', 'GTFO.EZ', 28808, '2026-10-19T22:37:16.677Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b11a181c-9b5d-460b-a0fd-196434716e23/agent-af34c935f3c86ea3c', 'G.EZ-T88', 'fix', 'GTFO.EZ', 26876, '2026-10-19T22:37:12.412Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b18c3d91-d1ee-498f-9b2a-ab33e3b645f2', 'GE-T10', 'feature', 'GTFO.EZ', 167981, '2026-10-15T10:07:24.869Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b2058fc7-0630-4bfb-8a82-e52eb10dc954', 'SS-T58', 'chore', 'Prose', 410975, '2026-10-15T19:31:24.035Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b30482d7-48dc-4494-907b-78377c6448fe', 'M.ORCH-T747', 'fix', 'MindAttic.Orchestrator', 43313, '2026-10-23T22:23:06.983Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b4b53ca8-20ef-43fa-8a06-0410b550846b', 'USS-T38', 'feature', 'UnitySolarSystem', 332571, '2026-10-15T11:15:54.182Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b6555e92-208c-40e1-97f9-9c2a416e26cb', 'GG-T21', 'chore', 'GridGameGaiden', 236676, '2026-10-16T02:25:11.71Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b69654b3-c0f4-4aee-be68-c271092bdc77', 'AUT-T8', 'chore', 'Automata', 157577, '2026-10-16T00:37:04.422Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b6fdf093-f759-4e26-9ada-b18103940feb', 'SS-T39', 'chore', 'Prose', 248565, '2026-10-15T13:00:53.667Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b71d1b8e-1724-4424-9fbd-b4f425f4eaf9', 'GG-T54', 'feature', 'GridGameGaiden', 213962, '2026-10-15T16:10:21.615Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b932aaec-57bd-4a58-8017-954514b6a5bc', 'U.SPC-T453', 'fix', 'Unity.SpaceGod', 30707, '2026-10-23T04:23:10.931Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bb51a166-5173-4d3e-8aad-2da24c961487', 'M.CAPT-T15', 'docs', 'MindAttic.Capture', 15339, '2026-10-21T18:11:13.275Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bbaff692-95da-4fb8-a66a-886e74765235', 'GG-T29', 'chore', 'GridGameGaiden', 74311, '2026-10-16T01:35:40.176Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc5df24e-48f0-44b2-a625-73ccd22895f3', 'SS-T64', 'chore', 'Prose', 57700, '2026-10-15T16:42:12.843Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bd617946-ff52-4e58-97ae-2a6cf5699b0b', 'SS-T93', 'chore', 'Prose', 460014, '2026-10-15T20:39:39.816Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bd8c4d39-d4df-435b-867b-23ae59638fa7', 'FOR-T2', 'chore', 'Formicarium', 93531, '2026-10-16T00:57:34.742Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bde4744f-6cf3-4e60-bc02-1f7ee03cc790', 'M.AUTH-T17', 'docs', 'MindAttic.Authentication', 17982, '2026-10-21T18:11:25.493Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bf1b6349-ffc0-42de-add6-324ba0d0392d', 'U.SPC-T23', 'fix', 'UnitySolarSystem', 180149, '2026-10-15T10:11:57.346Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bf586b10-0d9f-4fb8-80a9-5d86be5699da', 'M.ORCH-T707', 'fix', 'MindAttic.Orchestrator', 43376, '2026-10-23T20:29:54.809Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c00670f4-d3cc-4b5c-85f5-2f823a2161ad', 'M.EXPT-T6', 'docs', 'MindAttic.Export', 15011, '2026-10-21T18:10:19.137Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c03cdd6e-5737-4767-891e-bb40608ed9b8', 'M.LOGS-T2', 'chore', 'MindAttic.Log', 75415, '2026-10-16T01:07:08.683Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c2f81431-926d-4235-8fe4-173acc0fe0f9', 'UNI-T38', 'chore', 'Unity.Chain', 52167, '2026-10-24T04:07:18.25Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c3455761-86bf-4098-a74b-9e21446fa2d6', 'AWN-T12', 'docs', 'AutoWebNav', 15938, '2026-10-21T18:10:50.49Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c37f7fea-1468-4b2b-93dc-a71f1b751e81', 'USS-T43', 'feature', 'UnitySolarSystem', 242441, '2026-10-15T11:19:51.319Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c3db9249-ec7f-45e3-8b14-46ad9d50d82e', 'M.ORCH-T730', 'fix', 'MindAttic.Orchestrator', 4953, '2026-10-23T20:24:25.321Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c43716d5-08d2-46b9-87db-43a2daf8bb7c', 'GG-T49', 'feature', 'GridGameGaiden', 174079, '2026-10-15T15:41:29.028Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c52da005-91f4-4da7-b153-4e97df01bea7', 'SS-T44', 'chore', 'Prose', 162599, '2026-10-15T12:43:08.817Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-a46ce4173e063e0f2', 'UR-T9', 'feature', 'Unity.Ricochet', 368102, '2026-10-23T09:03:39.206Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-a479fb5ada2aed182', 'UR-T10', 'feature', 'Unity.Ricochet', 174776, '2026-10-23T08:48:59.241Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-a4a0409d0c14ba826', 'UR-T12', 'feature', 'Unity.Ricochet', 199677, '2026-10-23T10:36:51.426Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-a56843ff01f1f999e', 'UR-T15', 'feature', 'Unity.Ricochet', 313761, '2026-10-23T12:01:08.101Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-a5942919c4e8c1029', 'UR-T18', 'fix', 'Unity.Ricochet', 278827, '2026-10-23T13:08:47.121Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-a8ea4003ef4d578a2', 'UR-T14', 'feature', 'Unity.Ricochet', 148213, '2026-10-23T11:20:23.33Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-aa150fd81fdd6aa26', 'UR-T8', 'feature', 'Unity.Ricochet', 341246, '2026-10-23T07:03:32.622Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-aaee9917070c9258b', 'UR-T13', 'feature', 'Unity.Ricochet', 197970, '2026-10-23T10:42:46.321Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-ab2c6d363e17e0a13', 'UR-T17', 'fix', 'Unity.Ricochet', 118124, '2026-10-23T12:40:06.174Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-ab93a7fb124c5df9f', 'UR-T7', 'feature', 'Unity.Ricochet', 312849, '2026-10-23T05:57:44.26Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-abeded40af42e6b6d', 'UR-T11', 'feature', 'Unity.Ricochet', 245439, '2026-10-23T09:47:28.899Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-adb40ec41d5ddd7c0', 'UR-T6', 'feature', 'Unity.Ricochet', 462425, '2026-10-23T06:22:59.282Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2/agent-ade14231501235d63', 'UR-T16', 'fix', 'Unity.Ricochet', 111422, '2026-10-23T12:29:07.071Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c58803a5-abdb-4e40-9883-67b5dba97611', 'GAD-T3', 'chore', 'Gadfly', 47328, '2026-10-16T01:38:53.102Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c5e62040-e0d6-4d45-a156-0d02c2b94109', 'SS-T59', 'chore', 'Prose', 683558, '2026-10-15T20:37:35.05Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c5f41188-8db6-46f6-9f04-990af283f260', 'SS-T92', 'chore', 'Prose', 486373, '2026-10-15T20:48:37.006Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c5fe3a24-4fc9-48ca-8a0a-209cee258da6', 'AUT-T9', 'chore', 'Automata', 126187, '2026-10-16T00:57:22.561Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c68febc3-2b04-46fb-a62a-66baae06101c', 'U.SPC-T30', 'chore', 'UnitySolarSystem', 75939, '2026-10-15T09:56:54.439Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c7681bf4-9b26-4e18-bdcc-3ce3ca3b388d', 'GG-T17', 'chore', 'GridGameGaiden', 213018, '2026-10-16T02:16:19.076Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c79db824-3a0a-4e46-a46c-d548cbfb7f5d', 'FOR-T3', 'chore', 'Formicarium', 50950, '2026-10-16T01:28:41.25Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c9b83a5e-d92e-4b62-9070-cfd381b94189', 'RB-T61', 'fix', 'REA.Bloodlines', 30116, '2026-10-22T21:55:36.924Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cb9c1911-d0f7-4894-be18-193cf72add08', 'KP-T13', 'docs', 'KdpPublish', 16076, '2026-10-21T18:10:58.402Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cde93896-ba2d-4622-a65c-3ec191294700', 'GG-T24', 'chore', 'GridGameGaiden', 42571, '2026-10-16T01:20:19.355Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cded2062-18e3-41fd-9d4d-621f0e6b4ee0', 'RB-T59', 'fix', 'REA.Bloodlines', 20766, '2026-10-22T21:11:37.233Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cf74dc58-d5e2-4182-be0c-b4ab3a49c988', 'GG-T32', 'chore', 'GridGameGaiden', 129183, '2026-10-15T13:17:05.986Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d23cd7a2-8609-4f40-ae5f-e46c3982544d', 'U.INC-T14', 'fix', 'Unity.Incognito', 245705, '2026-10-23T00:50:11.418Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d2f65320-6698-4816-a87d-6d7dba20cce7', 'SS-T3', 'chore', 'Prose', 75422, '2026-10-15T11:58:04.783Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e', 'RB-T135', 'fix', 'MindAttic.Orchestrator', 3254721, '2026-10-23T13:23:31.734Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a0e17719414d6f4c8', 'M.ORCH-T631', 'chore', 'MindAttic.Orchestrator', 46531, '2026-10-23T01:03:32.604Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a33b19734d3971181', 'M.ORCH-T650', 'fix', 'MindAttic.Orchestrator', 59238, '2026-10-23T01:19:28.545Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a4e1fdbdee9971b9c', 'M.ORCH-T628', 'feature', 'MindAttic.Orchestrator', 54889, '2026-10-22T22:58:19.846Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a52e868208ac46add', 'M.ORCH-T637', 'feature', 'MindAttic.Orchestrator', 343931, '2026-10-22T23:11:33.767Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a5e6c1ea20eaff006', 'M.ORCH-T646', 'fix', 'MindAttic.Orchestrator', 53785, '2026-10-23T00:42:53.25Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a63d01ff37684451d', 'M.ORCH-T629', 'feature', 'MindAttic.Orchestrator', 106519, '2026-10-22T23:31:41.827Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a68074616b55b5b75', 'M.ORCH-T643', 'fix', 'MindAttic.Orchestrator', 42310, '2026-10-23T00:24:25.128Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a70446d43899f4492', 'M.ORCH-T651', 'fix', 'MindAttic.Orchestrator', 35015, '2026-10-23T01:15:40.19Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a7c3253f54e3eb3db', 'M.ORCH-T657', 'chore', 'MindAttic.Orchestrator', 71504, '2026-10-23T10:29:41.394Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a8cf7cb636fec618d', 'PRS-T133', 'chore', 'MindAttic.Orchestrator', 106981, '2026-10-23T10:36:27.679Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a8d57e91e281be715', 'M.ORCH-T653', 'fix', 'MindAttic.Orchestrator', 69755, '2026-10-23T01:43:45.922Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a8e05c29c9870c388', 'M.ORCH-T642', 'fix', 'MindAttic.Orchestrator', 61948, '2026-10-23T00:23:26.774Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a977c04827ce82822', 'M.ORCH-T640', 'fix', 'MindAttic.Orchestrator', 125007, '2026-10-22T23:39:37.696Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-a9a7af1d0e6531ecd', 'M.ORCH-T652', 'fix', 'MindAttic.Orchestrator', 48115, '2026-10-23T01:54:55.855Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-aa0a3df2976b600a4', 'M.ORCH-T647', 'fix', 'MindAttic.Orchestrator', 52219, '2026-10-23T01:06:14.635Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-ab3fdc001b5a60e26', 'M.ORCH-T630', 'feature', 'MindAttic.Orchestrator', 193420, '2026-10-23T00:06:46.139Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-ac84664c3525a92d9', 'M.ORCH-T644', 'fix', 'MindAttic.Orchestrator', 19910, '2026-10-23T00:25:00.074Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-ad88d402bb53da477', 'M.ORCH-T641', 'fix', 'MindAttic.Orchestrator', 103252, '2026-10-22T23:55:41.919Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-adb5bfd1475f73fd2', 'M.ORCH-T648', 'fix', 'MindAttic.Orchestrator', 21967, '2026-10-23T01:04:45.719Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-ae6cc239c7620e6bc', 'M.ORCH-T659', 'fix', 'MindAttic.Orchestrator', 183497, '2026-10-23T11:06:22.896Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d3469031-4b42-40c5-a2e3-58a88fb2b06e/agent-af58afe13ca1bd0b0', 'M.ORCH-T655', 'feature', 'MindAttic.Orchestrator', 700216, '2026-10-23T14:26:50.924Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-a32b62aba1c9e767a', 'G.EZ-T86', 'chore', 'GTFO.EZ', 49666, '2026-10-20T01:49:51.924Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-aa811cb908406606b', 'G.AWPN-T181', 'feature', 'GTFO.EZ', 79393, '2026-10-20T01:56:29.67Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-ab0597928274e16e7', 'G.EZ-T90', 'chore', 'GTFO.EZ', 110270, '2026-10-20T02:03:44.999Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d42c398d-dc09-4b16-bee5-d3156a7dcec1/agent-ac3ad88d78b3f206d', 'G.AWPN-T183', 'feature', 'GTFO.EZ', 60197, '2026-10-20T01:57:10.404Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d442eb2a-5100-4bee-81bb-b96218fd8e43', 'OC-T2', 'chore', 'OpenCredentials', 133990, '2026-10-16T01:22:22.22Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d4b09c9c-5532-4ca9-b959-5666a48be734', 'MB-T17', 'docs', 'MediaButler', 16971, '2026-10-21T18:11:23.811Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d528c4b0-ef4c-4c88-891b-eb12780284b6', 'AWN-T2', 'chore', 'AutoWebNav', 68820, '2026-10-16T00:55:29.283Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d5d51d7a-38fc-48c9-8d71-f82a76643098', 'GGC-T2', 'chore', 'GridGameCaravan', 61874, '2026-10-16T01:00:38.379Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d61c1e3d-8cc7-413e-a856-63d23aead583', 'UH-T40', 'chore', 'Unity.Hare', 197112, '2026-10-24T10:40:57.381Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a07acc644daf80178', 'RB-T58', 'feature', 'REA.Bloodlines', 299774, '2026-10-23T16:35:55.628Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a16f4ca1a9fb7bf70', 'RB-T154', 'fix', 'REA.Bloodlines', 99361, '2026-10-23T17:06:02.953Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a276d22d2cda89f56', 'RB-T77', 'feature', 'REA.Bloodlines', 103108, '2026-10-23T19:44:46.321Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a4a3fb0f934ce4db6', 'RB-T71', 'feature', 'REA.Bloodlines', 229226, '2026-10-23T19:46:00.695Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a570d32bbc2e95cac', 'RB-T75', 'feature', 'REA.Bloodlines', 188326, '2026-10-23T19:43:53.624Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a597736302885f77b', 'RB-T70', 'feature', 'REA.Bloodlines', 615585, '2026-10-23T18:30:08.052Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a59be0406b8b0f0ef', 'RB-T152', 'chore', 'REA.Bloodlines', 23670, '2026-10-23T17:10:06.841Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a6bbdd794dc06c481', 'RB-T62', 'feature', 'REA.Bloodlines', 433914, '2026-10-23T17:09:07.762Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a7ad329e32332731d', 'RB-T97', 'feature', 'REA.Bloodlines', 123594, '2026-10-23T19:44:42.915Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a8939e428a9dd46ba', 'RB-T155', 'fix', 'REA.Bloodlines', 63417, '2026-10-23T17:09:32.444Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a8d643952dd09957f', 'RB-T80', 'feature', 'REA.Bloodlines', 155969, '2026-10-23T19:44:56.252Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-a9abe170c07e77dea', 'RB-T74', 'feature', 'REA.Bloodlines', 160791, '2026-10-23T19:44:45.174Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-aa6da68f32c85fbc4', 'RB-T66', 'feature', 'REA.Bloodlines', 268469, '2026-10-23T16:46:35.443Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-ab333f90262183a18', 'RB-T63', 'feature', 'REA.Bloodlines', 224227, '2026-10-23T16:44:05.66Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-ab33d869aa3dab0dc', 'RB-T67', 'feature', 'REA.Bloodlines', 138137, '2026-10-23T16:45:17.199Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-aba471e78967d9a6d', 'RB-T76', 'feature', 'REA.Bloodlines', 174870, '2026-10-23T19:44:43.546Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-abd02446cf367d2a3', 'RB-T79', 'feature', 'REA.Bloodlines', 161657, '2026-10-23T19:44:41.556Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-ac9f8eeded416bb60', 'RB-T150', 'fix', 'REA.Bloodlines', 80002, '2026-10-23T16:19:20.945Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-aca7eaa1236f31c3c', 'RB-T64', 'feature', 'REA.Bloodlines', 186148, '2026-10-23T16:41:05.858Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-ad116c4ef1f86b1a8', 'RB-T86', 'feature', 'REA.Bloodlines', 129784, '2026-10-23T19:37:03.466Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-ad8abda21e621f1d4', 'RB-T68', 'feature', 'REA.Bloodlines', 552051, '2026-10-23T17:42:53.136Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-aeb018538ea842e5e', 'RB-T57', 'feature', 'REA.Bloodlines', 505578, '2026-10-23T16:57:09.915Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-aec6b4a58c5c0ee6e', 'RB-T65', 'feature', 'REA.Bloodlines', 213297, '2026-10-23T16:42:55.836Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-aef352f4fc9da86a3', 'RB-T72', 'feature', 'REA.Bloodlines', 199746, '2026-10-23T19:46:04.096Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-af1bd32314dba7be7', 'RB-T60', 'feature', 'REA.Bloodlines', 316993, '2026-10-23T16:38:48.194Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-af1e32e100253d9ac', 'RB-T69', 'feature', 'REA.Bloodlines', 636958, '2026-10-23T18:52:40.642Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d63b8323-0214-4476-ab5a-1700c5edbd3d/agent-afb0739900e434c3f', 'RB-T96', 'feature', 'REA.Bloodlines', 148634, '2026-10-23T19:44:40.946Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a017a5f478ed06a95', 'U.SPC-T304', 'fix', 'UnitySpace', 433076, '2026-10-20T01:39:32.635Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a1ae7c0cab83181f3', 'U.SPC-T308', 'fix', 'UnitySpace', 112381, '2026-10-20T01:17:47.548Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a4f53685d71f25de6', 'U.SPC-T302', 'fix', 'UnitySpace', 283252, '2026-10-20T01:42:15.568Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a5075c678650025e6', 'U.SPC-T305', 'fix', 'UnitySpace', 124950, '2026-10-20T01:11:09.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a878652d2b215db74', 'U.SPC-T301', 'fix', 'UnitySpace', 347053, '2026-10-20T01:39:32.116Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-a971f6020787a8ec1', 'U.SPC-T315', 'fix', 'UnitySpace', 146738, '2026-10-20T01:41:19.672Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-ae6e41aabbb78f407', 'U.SPC-T306', 'fix', 'UnitySpace', 637521, '2026-10-20T01:42:35.549Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-aec3aab9fefec46c5', 'U.SPC-T307', 'fix', 'UnitySpace', 281396, '2026-10-20T01:30:19.521Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d668d995-0336-42aa-97b7-fabadf33d573/agent-af06a0980b838f74e', 'U.SPC-T303', 'fix', 'UnitySpace', 396375, '2026-10-20T01:35:57.683Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d7f7f252-a846-4864-9e8e-389c22c4fa38', 'U.SPC-T516', 'fix', 'Unity.SpaceGod', 19345, '2026-10-24T00:25:14.65Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d9285b98-581c-48e0-a995-2068e04bece3', 'M.ORCH-T729', 'fix', 'MindAttic.Orchestrator', 14978, '2026-10-23T20:20:43.781Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4addfb-4d3b-45a7-9ac2-828ff4b1782c', 'M.ORCH-T74', 'fix', 'MindAttic.Orchestrator', 52787, '2026-10-15T07:21:38.006Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd298eb1-9d24-4719-b110-46982a4ea713', 'GG-T27', 'chore', 'GridGameGaiden', 60700, '2026-10-16T00:39:19.341Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd5950c7-aab0-40f6-9e36-b0e6e19c1693', 'GG-T35', 'feature', 'GridGameGaiden', 268757, '2026-10-15T13:58:02.414Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd813c1d-56e7-4a98-bb5e-7f25a8041a2c/agent-a6c38790f53cbe880', 'UH-T77', 'feature', 'Unity.Hare', 216486, '2026-10-24T11:23:56.184Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd8e4d1e-8aa0-4bd0-a754-73b1b2b2fa3c', 'SS-T62', 'chore', 'Prose', 191484, '2026-10-15T17:48:34.799Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddecf505-0c91-42f1-bcd2-81b044ce919e', 'GG-T31', 'chore', 'GridGameGaiden', 129061, '2026-10-15T13:03:13.336Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a01f6be6495627bae', 'M.ORCH-T793', 'feature', 'MindAttic.Orchestrator', 65211, '2026-10-24T17:28:07.817Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a258d993c85fe92ab', 'M.COMP-T57', 'fix', 'MindAttic.Orchestrator', 30824, '2026-10-24T17:47:40.28Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a3eef508d237eba8c', 'M.ORCH-T786', 'feature', 'MindAttic.Orchestrator', 408808, '2026-10-24T17:29:37.989Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a4d3ee4e67aee79c5', 'M.ORCH-T795', 'fix', 'MindAttic.Orchestrator', 140558, '2026-10-24T17:36:11.415Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a515d7466ebeca05c', 'M.ORCH-T763', 'feature', 'MindAttic.Orchestrator', 106716, '2026-10-24T13:43:18.756Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a629fcfc5dc87e80a', 'M.ORCH-T783', 'fix', 'MindAttic.Orchestrator', 332441, '2026-10-24T16:38:35.189Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a6cd69cd9aac095ae', 'M.ORCH-T787', 'fix', 'MindAttic.Orchestrator', 96958, '2026-10-24T16:33:02.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a822fbe43002eea05', 'M.ORCH-T798', 'fix', 'MindAttic.Orchestrator', 77504, '2026-10-24T17:42:46.664Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aa9764be281d284a7', 'M.ORCH-T784', 'fix', 'MindAttic.Orchestrator', 205617, '2026-10-24T16:34:24.92Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aab894acbfa8bb162', 'M.COMP-T54', 'fix', 'MindAttic.Orchestrator', 203819, '2026-10-24T18:02:11.94Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-abd51d823b4ca01f0', 'M.ORCH-T796', 'feature', 'MindAttic.Orchestrator', 102930, '2026-10-24T17:39:20.074Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-abe3b3178eed1a362', 'M.ORCH-T785', 'feature', 'MindAttic.Orchestrator', 163121, '2026-10-24T17:13:29.466Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ad2e2776ac35b6698', 'M.ORCH-T762', 'feature', 'MindAttic.Orchestrator', 902219, '2026-10-24T15:02:59.219Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ae9920bc91bd5d695', 'M.ORCH-T790', 'feature', 'MindAttic.Orchestrator', 467255, '2026-10-24T17:25:59.867Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aed313d6d059581d9', 'M.ORCH-T797', 'feature', 'MindAttic.Orchestrator', 111541, '2026-10-24T17:41:03.12Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af480dbdf5f6f9a65', 'M.ORCH-T801', 'fix', 'MindAttic.Orchestrator', 240473, '2026-10-24T18:02:02.478Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af59ff3edd6059567', 'M.ORCH-T788', 'fix', 'MindAttic.Orchestrator', 290193, '2026-10-24T16:51:25.114Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af9b5e9306dd3d88b', 'M.ORCH-T794', 'fix', 'MindAttic.Orchestrator', 107776, '2026-10-24T17:49:27.535Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a173f4a8fb2b9b60e', 'UH-T14', 'feature', 'Unity.Hare', 369989, '2026-10-23T10:54:39.407Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a452c29d99cd981bc', 'UH-T9', 'feature', 'Unity.Hare', 187568, '2026-10-23T05:51:43.855Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a474f76ba55f3bf2a', 'UH-T8', 'feature', 'Unity.Hare', 115459, '2026-10-23T05:41:51.486Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a68bac30ccc561b32', 'UH-T15', 'feature', 'Unity.Hare', 266183, '2026-10-23T11:14:51.429Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-aadc848a6a4999e07', 'UH-T13', 'feature', 'Unity.Hare', 165707, '2026-10-23T08:48:05.041Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-ac7c3b9b9cc688def', 'UH-T10', 'feature', 'Unity.Hare', 196597, '2026-10-23T06:02:11.959Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-ad33e70b0f0ee69fd', 'UH-T11', 'feature', 'Unity.Hare', 167681, '2026-10-23T06:56:17.972Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-adf673285c60351fd', 'UH-T12', 'feature', 'Unity.Hare', 241208, '2026-10-23T07:26:43.195Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dff9327a-0ca8-4ec0-882d-c5d913912a33', 'M.ORCH-T659', 'fix', 'MindAttic.Orchestrator', 31979, '2026-10-23T11:04:31.842Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e1115a1c-8787-44f6-9ae4-72c58c0d8c4a', 'M.ORCH-T743', 'fix', 'MindAttic.Orchestrator', 178710, '2026-10-23T20:43:01.789Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e1b11a13-af07-4894-afd8-bb45ee1b31ab', 'WF-T30', 'chore', 'MindAttic.Workflow', 370546, '2026-10-16T00:45:01.757Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e268cede-7033-44a0-9cde-56644a25b8f3', 'M.IDEA-T20', 'docs', 'MindAttic.Ideas', 17791, '2026-10-21T18:11:39.062Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e312143d-5d79-4ec4-84d6-1da06a4944ed', 'U.SPC-T29', 'chore', 'UnitySolarSystem', 43459, '2026-10-15T09:54:26.893Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e3a783cb-464f-4dad-b7fc-d60aac35c008', 'SS-T91', 'chore', 'Prose', 232583, '2026-10-15T19:48:48.447Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e4584685-15bf-4183-a6a9-236732441d16', 'M.ORCH-T725', 'fix', 'MindAttic.Orchestrator', 18117, '2026-10-23T20:23:15.97Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e653ecca-2b4a-4e49-8c9a-a2070d965550', 'M.PSST-T16', 'docs', 'MindAttic.Psst', 18693, '2026-10-21T18:11:18.172Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e765c806-dd7c-4542-87fb-075508a802b3', 'U.SPC-T32', 'chore', 'UnitySolarSystem', 134712, '2026-10-15T11:07:31.908Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e836982c-1c81-453c-9f34-a86bd75201ab', 'SS-T43', 'chore', 'Prose', 159035, '2026-10-15T12:47:30.612Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e97a0688-750c-4157-9ff6-802b3460b327', 'GG-T33', 'chore', 'GridGameGaiden', 155465, '2026-10-15T13:27:22.046Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ea19ab98-b204-4b60-882a-3ba6753d94d1', 'GG-T17', 'chore', 'GridGameGaiden', 33842, '2026-10-15T11:19:50.931Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a01a22015f084f728', 'U.SPC-T491', 'feature', 'Unity.SpaceGod', 1007688, '2026-10-24T01:58:07.118Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a099755eb88f083aa', 'U.SPC-T498', 'feature', 'Unity.SpaceGod', 481513, '2026-10-24T01:59:10.027Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a1778c0b77a3dc07f', 'U.SPC-T516', 'fix', 'Unity.SpaceGod', 351417, '2026-10-24T01:13:35.24Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a187e4cbe9089e070', 'U.SPC-T490', 'feature', 'Unity.SpaceGod', 1246329, '2026-10-24T01:52:57.526Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a1a85270a1c2b87fd', 'U.SPC-T515', 'fix', 'Unity.SpaceGod', 201700, '2026-10-24T00:46:46.558Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a1fa20dc6768b7bed', 'U.SPC-T513', 'fix', 'Unity.SpaceGod', 32390, '2026-10-24T00:13:53.343Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a2952836a79c522eb', 'U.SPC-T514', 'fix', 'Unity.SpaceGod', 187166, '2026-10-24T01:02:29.244Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a2992705a3c1ecb46', 'U.SPC-T486', 'feature', 'Unity.SpaceGod', 478882, '2026-10-24T01:58:45.48Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a39b470cf9b699729', 'U.SPC-T468', 'feature', 'Unity.SpaceGod', 342864, '2026-10-24T01:35:07.603Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a4154db48ac99c341', 'U.SPC-T496', 'feature', 'Unity.SpaceGod', 111564, '2026-10-24T01:22:09.305Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a7561022fac8705c1', 'U.SPC-T485', 'feature', 'Unity.SpaceGod', 441358, '2026-10-24T01:48:18.187Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a960ee211cc2bb5a0', 'U.SPC-T508', 'feature', 'Unity.SpaceGod', 307672, '2026-10-24T01:57:41.327Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-a9ce5482f4e0d7a32', 'U.SPC-T476', 'feature', 'Unity.SpaceGod', 659078, '2026-10-24T01:44:34.188Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-ab0fdde4cbc5a79b4', 'U.SPC-T473', 'feature', 'Unity.SpaceGod', 486863, '2026-10-24T00:44:22.346Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-ab785bea2116ecf8b', 'U.SPC-T499', 'feature', 'Unity.SpaceGod', 284162, '2026-10-24T01:51:48.71Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-ac024e6173758e11f', 'U.SPC-T527', 'fix', 'Unity.SpaceGod', 30726, '2026-10-24T01:54:26.406Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-ac58a9b0697b5e51b', 'U.SPC-T474', 'feature', 'Unity.SpaceGod', 705047, '2026-10-24T01:52:00.103Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-ae8ff5bd8970e3324', 'U.SPC-T475', 'feature', 'Unity.SpaceGod', 324957, '2026-10-24T00:36:48.489Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-aec3a34d5ba538819', 'U.SPC-T512', 'fix', 'Unity.SpaceGod', 28987, '2026-10-24T00:13:26.862Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-aecbc74534675d703', 'U.SPC-T511', 'fix', 'Unity.SpaceGod', 49337, '2026-10-24T00:15:04.9Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-aee579c29f6f4e1af', 'U.SPC-T495', 'feature', 'Unity.SpaceGod', 26688, '2026-10-24T01:15:11.27Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-af5287143d53fcb79', 'U.SPC-T510', 'fix', 'Unity.SpaceGod', 38614, '2026-10-24T00:13:58.398Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-af83a180f6a56170c', 'U.SPC-T492', 'feature', 'Unity.SpaceGod', 294595, '2026-10-24T01:39:44.12Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-afa64b55e44dd27af', 'U.SPC-T494', 'feature', 'Unity.SpaceGod', 150242, '2026-10-24T01:24:07.503Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ebfa0b7b-24aa-466f-a83a-3b4d3a262661/agent-afdd6447f73518b99', 'U.SPC-T466', 'feature', 'Unity.SpaceGod', 738429, '2026-10-24T00:49:59.138Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ec1d417b-295e-4eba-b0ab-329285e64d52/agent-a72c53e5f0364b209', 'M.TIX-T146', 'chore', 'MindAttic.Tickets', 63600, '2026-10-24T11:15:34.899Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ec40fb80-b7fe-45b0-b61d-f93943e1f32d', 'U.SPC-T36', 'chore', 'UnitySolarSystem', 69005, '2026-10-15T10:41:33.507Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ec47550e-6d10-4b00-8533-e1b3eb053924', 'UQD-T31', 'chore', 'Unity.QuickDuel', 37199, '2026-10-24T13:12:10.069Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ecc2aeed-abb0-4e6e-9860-28f3394e89eb', 'U.SPC-T516', 'fix', 'Unity.SpaceGod', 5365, '2026-10-24T00:26:32.139Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ed70be5d-93e8-48f1-8d68-f29ae5a92946', 'CUR-T3', 'chore', 'Cursory', 54273, '2026-10-16T01:29:26.778Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ee923b3d-97ef-4a2e-a598-a6fa4d2d9e47', 'M.ORCH-T591', 'fix', 'MindAttic.Orchestrator', 9257, '2026-10-21T19:29:09.662Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eff6de0b-fb07-4fd8-881f-6cd6e857cb2f', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 69580, '2026-10-22T02:13:34.57Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f0ff95e0-534d-49ca-a0ba-a8ba14a45b67', 'SS-T86', 'chore', 'Prose', 513473, '2026-10-15T20:48:46.831Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f1629c56-25ac-474b-9147-a5195ff2346f', 'GG-T83', 'docs', 'Unity.GridGame', 18259, '2026-10-21T18:11:57.923Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3171e38-8301-485a-83ca-0edfd769511a', 'RB-T46', 'fix', 'REA.Bloodlines', 12783, '2026-10-22T18:14:12.448Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f360cb66-220f-4164-924e-914c343fb2df', 'M.ORCH-T741', 'fix', 'MindAttic.Orchestrator', 16073, '2026-10-23T20:31:14.416Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3b42562-0547-4978-a855-a515a59cdd11', 'AUT-T6', 'chore', 'Automata', 89218, '2026-10-16T00:22:14.127Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3be53cc-8f76-4bcc-8b71-dcf93a075bad/agent-a05a2aba5d1c068e5', 'U.SPC-T363', 'fix', 'Unity.SpaceGod', 175423, '2026-10-21T07:18:15.172Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3be53cc-8f76-4bcc-8b71-dcf93a075bad/agent-a35f6a570139b98fc', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 493324, '2026-10-21T12:51:51.812Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3be53cc-8f76-4bcc-8b71-dcf93a075bad/agent-a478a0a595f2d0f57', 'U.SPC-T331', 'docs', 'Unity.SpaceGod', 74403, '2026-10-21T04:49:58.685Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3be53cc-8f76-4bcc-8b71-dcf93a075bad/agent-a903eb3d7e9c00144', 'U.SPC-T366', 'fix', 'Unity.SpaceGod', 190631, '2026-10-21T12:38:00.806Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f3d1629b-17ef-424e-9dcd-c5976cbbb30f', 'UQD-T28', 'chore', 'Unity.QuickDuel', 53121, '2026-10-24T01:14:07.034Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f428d8f4-8973-457f-abc5-c5134b13d89e', 'M.ORCH-T673', 'fix', 'MindAttic.Orchestrator', 90801, '2026-10-23T16:31:35.988Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f6684589-5e76-4409-b91e-074791c5adf3', 'M.MDIA-T2', 'chore', 'MindAttic.Media', 58402, '2026-10-16T01:05:45.495Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f6f83746-4a80-469e-88fe-1661ddf8d454', 'JH-T14', 'docs', 'JobHunt', 16081, '2026-10-21T18:11:07.545Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f82e3696-b3df-4dec-ab39-ae8e360e24c3', 'RB-T132', 'chore', 'REA.Bloodlines', 8745, '2026-10-24T00:09:28.848Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f987f05c-471f-44a8-a640-ff86f01ed7bb', 'M.ORCH-T652', 'fix', 'MindAttic.Orchestrator', 8317, '2026-10-23T01:51:50.315Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fa118d1e-604d-4f79-abe1-27b253de191b', 'MAT-T50', 'fix', 'MindAttic.Agency', 44080, '2026-10-24T06:46:10.765Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fa5f7470-6166-4c23-8566-2888d7039173', 'U.INC-T20', 'chore', 'Unity.Incognito', 24771, '2026-10-24T01:13:53.044Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('facfb962-efa9-445d-aff7-67252a0f51fe', 'GE-T10', 'feature', 'GTFO.EZ', 133135, '2026-10-15T09:00:29.513Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fb98ff00-8878-4a14-b8d8-56b5088f0d17', 'M.ORCH-T731', 'fix', 'MindAttic.Orchestrator', 26227, '2026-10-23T20:21:38.697Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a0876261cf2c71884', 'RB-T113', 'feature', 'REA.Bloodlines', 534074, '2026-10-24T00:40:15.887Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a0db41697fb1fc230', 'RB-T112', 'feature', 'REA.Bloodlines', 391793, '2026-10-24T00:35:06.388Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a1255bdf96fb4ed7b', 'RB-T106', 'feature', 'REA.Bloodlines', 335627, '2026-10-24T00:33:13.616Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a19b004ea865e966d', 'RB-T98', 'feature', 'REA.Bloodlines', 350882, '2026-10-24T00:50:33.773Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a1e5dbe91cbbef4c0', 'RB-T104', 'feature', 'REA.Bloodlines', 290335, '2026-10-24T00:38:17.852Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a33dd331c0f509875', 'RB-T161', 'fix', 'REA.Bloodlines', 90396, '2026-10-24T00:25:43.287Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a4535520aac756f70', 'RB-T160', 'fix', 'REA.Bloodlines', 73194, '2026-10-24T00:22:31.558Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a516ebffcfb8e9d6d', 'RB-T111', 'feature', 'REA.Bloodlines', 419104, '2026-10-24T00:51:03.875Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a5cd4b65eaa9142ad', 'RB-T108', 'feature', 'REA.Bloodlines', 503010, '2026-10-24T00:54:36.372Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a63b599a87a0f39ae', 'RB-T159', 'fix', 'REA.Bloodlines', 61175, '2026-10-24T00:11:33.66Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a72326c6e6e38d9b3', 'RB-T158', 'fix', 'REA.Bloodlines', 121311, '2026-10-24T00:27:20.297Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-a7b3ae3fb590b9dcc', 'RB-T107', 'feature', 'REA.Bloodlines', 498616, '2026-10-24T00:43:26.45Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-aa8a6f7694f913616', 'RB-T110', 'feature', 'REA.Bloodlines', 707786, '2026-10-24T00:50:57.5Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-ab5df2d516cfe69a7', 'RB-T109', 'feature', 'REA.Bloodlines', 398414, '2026-10-24T00:37:38.075Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-ab61ac6356f44b2fb', 'RB-T105', 'feature', 'REA.Bloodlines', 141210, '2026-10-24T00:23:21.412Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-ac3977b41e70269b0', 'RB-T132', 'chore', 'REA.Bloodlines', 55194, '2026-10-24T00:09:59.01Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc6a58a5-b922-417b-8bb7-acf8d5519d89/agent-ae0757d26d02fc032', 'RB-T129', 'chore', 'REA.Bloodlines', 194665, '2026-10-24T00:20:04.864Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fc72c7bf-0b3c-468b-a1b9-9b2056fd39ac', 'M.TELE-T2', 'chore', 'MindAttic.Telemetry', 123716, '2026-10-16T01:13:19.81Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fe936aaf-26ea-444e-ac86-dde15736393e', 'UR-T34', 'chore', 'Unity.Ricochet', 84914, '2026-10-23T23:37:09.162Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ffb7b0e5-451a-4d53-beb9-5d3f0fec0530', 'UNI-T21', 'fix', 'Unity.Chain', 28668, '2026-10-23T22:14:30.192Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ffd66af0-af61-4624-b5dd-1c85297d70e0', 'UQD-T23', 'fix', 'Unity.QuickDuel', 16361, '2026-10-23T10:45:13.123Z', 1, 'opus');

-- settings
INSERT INTO settings (name, value) VALUES ('Assignees', '{}');
INSERT INTO settings (name, value) VALUES ('AuditEveryHours', '24');
INSERT INTO settings (name, value) VALUES ('AutoLogin', 'false');
INSERT INTO settings (name, value) VALUES ('Autopilot', 'true');
INSERT INTO settings (name, value) VALUES ('AutopilotHandMinutes', '15');
INSERT INTO settings (name, value) VALUES ('AutopilotSkip', '[]');
INSERT INTO settings (name, value) VALUES ('BlockedBy', '{}');
INSERT INTO settings (name, value) VALUES ('ContextCyclePercent', '80');
INSERT INTO settings (name, value) VALUES ('DefaultProvider', '"Claude"');
INSERT INTO settings (name, value) VALUES ('DiskCriticalGb', '5');
INSERT INTO settings (name, value) VALUES ('DiskWarnGb', '20');
INSERT INTO settings (name, value) VALUES ('ExemptCommands', '["/pacing","/deploy","/run"]');
INSERT INTO settings (name, value) VALUES ('GitHubIntake', 'false');
INSERT INTO settings (name, value) VALUES ('GroomAfterDays', '30');
INSERT INTO settings (name, value) VALUES ('KeepGoing', 'true');
INSERT INTO settings (name, value) VALUES ('MaxAgentDelaySeconds', '300');
INSERT INTO settings (name, value) VALUES ('MaxConcurrentSessions', '6');
INSERT INTO settings (name, value) VALUES ('MaxSessionsPerRepo', '2');
INSERT INTO settings (name, value) VALUES ('MaxSubagents', '16');
INSERT INTO settings (name, value) VALUES ('ModelOverrides', '{}');
INSERT INTO settings (name, value) VALUES ('OrphanQuietMinutes', '10');
INSERT INTO settings (name, value) VALUES ('OrphanRedispatches', '0');
INSERT INTO settings (name, value) VALUES ('PaceCurveK', '0.1');
INSERT INTO settings (name, value) VALUES ('PaceTargetPercent', '98');
INSERT INTO settings (name, value) VALUES ('Pacing', 'true');
INSERT INTO settings (name, value) VALUES ('ProviderCapPercent', '90');
INSERT INTO settings (name, value) VALUES ('QuietHours', '[]');
INSERT INTO settings (name, value) VALUES ('RebalanceLogins', 'false');
INSERT INTO settings (name, value) VALUES ('RepoBudgetPercent', '{}');
INSERT INTO settings (name, value) VALUES ('RepoMaxSessions', '{}');
INSERT INTO settings (name, value) VALUES ('RepoOrgs', '{}');
INSERT INTO settings (name, value) VALUES ('Reservations', '{}');
INSERT INTO settings (name, value) VALUES ('ResumeAfterReset', 'true');
INSERT INTO settings (name, value) VALUES ('ReviewDiffLines', '300');
INSERT INTO settings (name, value) VALUES ('RiskyFiles', '["*AgentGate*.cs","*pace-gate*","*context-gate*","*commit-msg*","*/hooks/*","*/Budget/Pacer.cs","*/Budget/BudgetCalculator.cs","*/Budget/PaceLedger*.cs","*/Scheduling/SubagentShare.cs","*RateLimitStatus*","*/Scheduling/Dispatcher.cs","*/Scheduling/Autopilot.cs","*land*.ps1","*deploy.ps1"]');
INSERT INTO settings (name, value) VALUES ('StaleAfterHours', '12');
INSERT INTO settings (name, value) VALUES ('StoryApproved', '[]');
INSERT INTO settings (name, value) VALUES ('StoryPriorities', '{}');
INSERT INTO settings (name, value) VALUES ('TabBlinkTokens', '1000');
INSERT INTO settings (name, value) VALUES ('Theme', '"Dark"');
INSERT INTO settings (name, value) VALUES ('WarnPercent', '80');
INSERT INTO settings (name, value) VALUES ('WeeklyAwareLinePercent', '75');
INSERT INTO settings (name, value) VALUES ('WeeklyReservePercent', '10');
INSERT INTO settings (name, value) VALUES ('WorktreeRoot', '"D:\\Projects\\MindAttic.worktrees"');

-- assignments
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (0, 'JFP-T6', 'JellyFinPoster', 'D:\Projects\MindAttic\JellyFinPoster', 'Claude', '2026-10-21T18:10:05.9542758Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (1, 'M.EXPT-T6', 'MindAttic.Export', 'D:\Projects\MindAttic\MindAttic.Export', 'Claude', '2026-10-21T18:10:08.6405169Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (2, 'CM-T7', 'ChiMesh', 'D:\Projects\MindAttic\ChiMesh', 'Claude', '2026-10-21T18:10:10.2751456Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (3, 'CLA-T7', 'Claudia', 'D:\Projects\MindAttic\Claudia', 'Claude', '2026-10-21T18:10:11.7444076Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (4, 'JF-T7', 'JoyceFit', 'D:\Projects\MindAttic\JoyceFit', 'Claude', '2026-10-21T18:10:13.106497Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (5, 'M.CRYP-T7', 'MindAttic.Cryptography', 'D:\Projects\MindAttic\MindAttic.Cryptography', 'Claude', '2026-10-21T18:10:14.5521435Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (6, 'M.HELP-T7', 'MindAttic.Helpers', 'D:\Projects\MindAttic\MindAttic.Helpers', 'Claude', '2026-10-21T18:10:16.2638928Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (7, 'M.BRWS-T9', 'MindAttic.Browser', 'D:\Projects\MindAttic\MindAttic.Browser', 'Claude', '2026-10-21T18:10:19.0861142Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (8, 'M.MDIA-T10', 'MindAttic.Media', 'D:\Projects\MindAttic\MindAttic.Media', 'Claude', '2026-10-21T18:10:20.9014276Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (9, 'M.TELE-T10', 'MindAttic.Telemetry', 'D:\Projects\MindAttic\MindAttic.Telemetry', 'Claude', '2026-10-21T18:10:24.143221Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (10, 'M.WEB-T10', 'MindAttic.Web', 'D:\Projects\MindAttic\MindAttic.Web', 'Claude', '2026-10-21T18:10:25.5414827Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (11, 'SET-T10', 'SyntheticExchangeTheory', 'D:\Projects\MindAttic\SyntheticExchangeTheory', 'Claude', '2026-10-21T18:10:26.9451179Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (12, 'GAD-T11', 'Gadfly', 'D:\Projects\MindAttic\Gadfly', 'Claude', '2026-10-21T18:10:28.6746236Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (13, 'AWN-T12', 'AutoWebNav', 'D:\Projects\MindAttic\AutoWebNav', 'Claude', '2026-10-21T18:10:32.5147473Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (14, 'M.DPLY-T12', 'MindAttic.Deploy', 'D:\Projects\MindAttic\MindAttic.Deploy', 'Claude', '2026-10-21T18:10:34.6298579Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (15, 'M.LOGS-T12', 'MindAttic.Log', 'D:\Projects\MindAttic\MindAttic.Log', 'Claude', '2026-10-21T18:10:38.8153496Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (16, 'KP-T13', 'KdpPublish', 'D:\Projects\MindAttic\KdpPublish', 'Claude', '2026-10-21T18:10:43.1142831Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (17, 'M.VALT-T13', 'MindAttic.Vault', 'D:\Projects\MindAttic\MindAttic.Vault', 'Claude', '2026-10-21T18:10:47.5098013Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (18, 'GGC-T13', 'TownWalker', 'D:\Projects\MindAttic\TownWalker', 'Claude', '2026-10-21T18:10:49.7200697Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (19, 'JH-T14', 'JobHunt', 'D:\Projects\MindAttic\JobHunt', 'Claude', '2026-10-21T18:10:51.8579607Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (20, 'CUR-T15', 'Cursory', 'D:\Projects\MindAttic\Cursory', 'Claude', '2026-10-21T18:10:54.060741Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (21, 'M.PSST-T16', 'MindAttic.Psst', 'D:\Projects\MindAttic\MindAttic.Psst', 'Claude', '2026-10-21T18:10:58.4371268Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (22, 'TT-T16', 'ThinkTank', 'D:\Projects\MindAttic\ThinkTank', 'Claude', '2026-10-21T18:11:00.8032318Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (23, 'AUT-T17', 'Automata', 'D:\Projects\MindAttic\Automata', 'Claude', '2026-10-21T18:11:02.9767031Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (24, 'MB-T17', 'MediaButler', 'D:\Projects\MindAttic\MediaButler', 'Claude', '2026-10-21T18:11:06.1973246Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (25, 'M.AUTH-T17', 'MindAttic.Authentication', 'D:\Projects\MindAttic\MindAttic.Authentication', 'Claude', '2026-10-21T18:11:08.4360905Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (26, 'TRC-T17', 'TaxRateCollector', 'D:\Projects\MindAttic\TaxRateCollector', 'Claude', '2026-10-21T18:11:10.6831902Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (27, 'FOR-T18', 'Formicarium', 'D:\Projects\MindAttic\Formicarium', 'Claude', '2026-10-21T18:11:12.6633823Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (28, 'M.IDEA-T20', 'MindAttic.Ideas', 'D:\Projects\MindAttic\MindAttic.Ideas', 'Claude', '2026-10-21T18:11:18.8769911Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (29, 'M.LEGN-T20', 'MindAttic.Legion', 'D:\Projects\MindAttic\MindAttic.Legion', 'Claude', '2026-10-21T18:11:21.0757047Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (30, 'OC-T20', 'OpenCredentials', 'D:\Projects\MindAttic\OpenCredentials', 'Claude', '2026-10-21T18:11:23.174772Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (31, 'TUT-T21', 'Tutor', 'D:\Projects\MindAttic\Tutor', 'Claude', '2026-10-21T18:11:25.1692619Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (32, 'IP-T22', 'IdiotProof', 'D:\Projects\MindAttic\IdiotProof', 'Claude', '2026-10-21T18:11:27.4590838Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (33, 'GG-T83', 'Unity.GridGame', 'D:\Projects\MindAttic\Unity.GridGame', 'Claude', '2026-10-21T18:11:35.2378989Z', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (34, 'UC-T34', 'Unity.Cursory', 'D:\Projects\MindAttic.worktrees\Unity.Cursory\UC-T34', 'Claude', '2026-10-23T05:21:17.2034141Z', 1, NULL, NULL, NULL, 32696, 'e84a8dcc-2f07-43b6-a4f5-c7a148ec4cff', 'opus', NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (35, 'UM-T74', 'Unity.Multiplayer', 'D:\Projects\MindAttic.worktrees\Unity.Multiplayer\UM-T74', 'Claude', '2026-10-23T05:21:23.9281867Z', 1, NULL, NULL, NULL, 61948, '555266d3-de95-4a86-beb2-152b31adedd1', 'opus', NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (36, 'U.SPC-T486', 'Unity.SpaceGod', 'D:\Projects\MindAttic.worktrees\Unity.SpaceGod\U.SPC-T486', 'Claude', '2026-10-24T01:06:04.8822663Z', 1, NULL, NULL, NULL, 73436, 'ebfa0b7b-24aa-466f-a83a-3b4d3a262661', 'opus', NULL, 'ryandebraal@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (37, 'U.SPC-T491', 'Unity.SpaceGod', 'D:\Projects\MindAttic.worktrees\Unity.SpaceGod\U.SPC-T491', 'Claude', '2026-10-24T01:13:42.7077773Z', 1, NULL, NULL, NULL, 73436, 'ebfa0b7b-24aa-466f-a83a-3b4d3a262661', 'opus', NULL, 'ryandebraal@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (38, 'U.SPC-T498', 'Unity.SpaceGod', 'D:\Projects\MindAttic.worktrees\Unity.SpaceGod\U.SPC-T498', 'Claude', '2026-10-24T01:15:39.3857618Z', 1, NULL, NULL, NULL, 73436, 'ebfa0b7b-24aa-466f-a83a-3b4d3a262661', 'opus', NULL, 'ryandebraal@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (39, 'M.LNCH-T23', 'MindAttic.Launcher', 'D:\Projects\MindAttic.worktrees\MindAttic.Launcher\M.LNCH-T23', 'Claude', '2026-10-24T03:56:37.1332515Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (40, 'RB-T152', 'REA.Bloodlines', 'D:\Projects\MindAttic.worktrees\REA.Bloodlines\RB-T152', 'Claude', '2026-10-24T03:56:38.8624778Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (41, 'M.CAPT-T12', 'MindAttic.Capture', 'D:\Projects\MindAttic.worktrees\MindAttic.Capture\M.CAPT-T12', 'Claude', '2026-10-24T03:59:43.8422976Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (42, 'PRS-T133', 'Prose', 'D:\Projects\MindAttic.worktrees\Prose\PRS-T133', 'Claude', '2026-10-24T03:59:44.9448021Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (43, 'U.EVE-T19', 'Unity.Eve', 'D:\Projects\MindAttic.worktrees\Unity.Eve\U.EVE-T19', 'Claude', '2026-10-24T04:10:27.8573412Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (44, 'GG-T54', 'Unity.GridGame', 'D:\Projects\MindAttic.worktrees\Unity.GridGame\GG-T54', 'Claude', '2026-10-24T04:13:52.3179455Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (45, 'GG-T53', 'Unity.GridGame', 'D:\Projects\MindAttic.worktrees\Unity.GridGame\GG-T53', 'Claude', '2026-10-24T04:13:53.251735Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (46, 'M.ORCH-T774', 'MindAttic.Orchestrator', 'D:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T774', 'Claude', '2026-10-24T05:24:24.9122382Z', 1, NULL, NULL, NULL, 22844, '9976eacd-149e-4ad5-a16b-b31ecc8eb4cc', 'sonnet', '2026-10-24T06:08:02.0603565Z', 'ryandebraal@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (47, 'MAT-T49', 'MindAttic.Agency', 'D:\Projects\MindAttic.worktrees\MindAttic.Agency\MAT-T49', 'Claude', '2026-10-24T06:38:31.351098Z', 1, NULL, NULL, NULL, 11408, '3355791e-fb1c-436b-9d8e-7fb6aeda09fd', 'sonnet', NULL, 'honey@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (48, 'UH-T41', 'Unity.Hare', 'D:\Projects\MindAttic.worktrees\Unity.Hare\UH-T41', 'Claude', '2026-10-24T10:53:41.786846Z', 1, NULL, NULL, NULL, 46704, '920f73e2-defc-4a55-b788-dcf01eab5395', 'sonnet', NULL, 'honey@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (49, 'UR-T36', 'Unity.Ricochet', 'D:\Projects\MindAttic.worktrees\Unity.Ricochet\UR-T36', 'Claude', '2026-10-24T11:07:26.6291281Z', 1, NULL, NULL, NULL, 46704, '920f73e2-defc-4a55-b788-dcf01eab5395', 'sonnet', NULL, 'honey@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (50, 'U.OVW-T7', 'Unity.Overworld', 'D:\Projects\MindAttic.worktrees\Unity.Overworld\U.OVW-T7', 'Claude', '2026-10-24T11:36:53.9165725Z', 1, NULL, NULL, NULL, 72228, '94fd2733-b325-443d-adfc-d8c7ef9f5bdd', 'sonnet', NULL, 'ryandebraal@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (51, 'RB-T110', 'REA.Bloodlines', 'D:\Projects\MindAttic.worktrees\REA.Bloodlines\RB-T110', 'Claude', '2026-10-24T12:36:03.4614359Z', 1, NULL, NULL, NULL, 34068, '80266bf8-e700-42a4-8046-431131f55d29', 'opus', NULL, 'honey@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (52, 'M.COMP-T54', 'MindAttic.Components', 'D:\Projects\MindAttic.worktrees\MindAttic.Components\M.COMP-T54', 'Claude', '2026-10-24T17:46:30.834492Z', 1, NULL, NULL, NULL, 30244, 'ddefd224-ff9a-422d-b6e9-218728189540', 'sonnet', NULL, 'ryandebraal@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (53, 'UNI-T40', 'Unity.Chain', 'D:\Projects\MindAttic.worktrees\Unity.Chain\UNI-T40', 'Claude', '2026-10-24T17:52:09.906211Z', 1, NULL, NULL, NULL, 52100, '33e02961-5b42-446b-a988-ee46b3b0ee9d', 'opus', NULL, 'honey@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (54, 'M.ORCH-T801', 'MindAttic.Orchestrator', 'D:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T801', 'Claude', '2026-10-24T17:53:36.099208Z', 1, NULL, NULL, NULL, 30244, 'ddefd224-ff9a-422d-b6e9-218728189540', 'sonnet', NULL, 'ryandebraal@mindattic.com');

-- organizations

-- org_members

-- state
INSERT INTO state (name, value) VALUES ('api-failovers', '[]');
INSERT INTO state (name, value) VALUES ('audit', '{
  "RanUtc": "2026-10-23T22:16:21.0642099Z",
  "Took": "00:00:32.2200684",
  "ReposScanned": 65,
  "FilesScanned": 6353,
  "Drift": [
    {
      "File": ".prose/commands/show.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\show.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\show.md",
      "CanonicalCount": 51,
      "Total": 52,
      "Variants": 2,
      "Similarity": 0.022222222222222223,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Agency",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Agency\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.02564102564102564,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "SyntheticExchangeTheory",
      "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.031746031746031744,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Incognito",
      "Path": "D:\\Projects\\MindAttic\\Unity.Incognito\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.041666666666666664,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\quicksave.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md",
      "CanonicalCount": 27,
      "Total": 52,
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
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.044444444444444446,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Overworld",
      "Path": "D:\\Projects\\MindAttic\\Unity.Overworld\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.05128205128205128,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.ProjectVania",
      "Path": "D:\\Projects\\MindAttic\\Unity.ProjectVania\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.05128205128205128,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.GridGame",
      "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.05405405405405406,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.058823529411764705,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/progress.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\progress.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\progress.md",
      "CanonicalCount": 51,
      "Total": 52,
      "Variants": 2,
      "Similarity": 0.06451612903225806,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/queue.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\queue.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\queue.md",
      "CanonicalCount": 51,
      "Total": 52,
      "Variants": 2,
      "Similarity": 0.07142857142857142,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Eve",
      "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.07547169811320754,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "UnityRonin",
      "Path": "D:\\Projects\\MindAttic\\UnityRonin\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.07547169811320754,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\quickload.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md",
      "CanonicalCount": 27,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.08695652173913043,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/commit.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\commit.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\commit.md",
      "CanonicalCount": 51,
      "Total": 52,
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
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.10526315789473684,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.11764705882352941,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 52,
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
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "KdpPublish",
      "Path": "D:\\Projects\\MindAttic\\KdpPublish\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "ChiMesh",
      "Path": "D:\\Projects\\MindAttic\\ChiMesh\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Claudia",
      "Path": "D:\\Projects\\MindAttic\\Claudia\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Cursory",
      "Path": "D:\\Projects\\MindAttic\\Cursory\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "IdiotProof",
      "Path": "D:\\Projects\\MindAttic\\IdiotProof\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MediaButler",
      "Path": "D:\\Projects\\MindAttic\\MediaButler\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Helpers",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Helpers\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Ideas",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Ideas\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Launcher",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Launcher\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Legion",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Legion\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Psst",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "MindAttic.Vault",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "TaxRateCollector",
      "Path": "D:\\Projects\\MindAttic\\TaxRateCollector\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "ThinkTank",
      "Path": "D:\\Projects\\MindAttic\\ThinkTank\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Tutor",
      "Path": "D:\\Projects\\MindAttic\\Tutor\\.prose/commands\\deploy.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md",
      "CanonicalCount": 37,
      "Total": 52,
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
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.21052631578947367,
      "IsCustom": true
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Automata",
      "Path": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\do.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md",
      "CanonicalCount": 28,
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
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
      "Total": 52,
      "Variants": 3,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Chain",
      "Path": "D:\\Projects\\MindAttic\\Unity.Chain\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.2631578947368421,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "JobHunt",
      "Path": "D:\\Projects\\MindAttic\\JobHunt\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
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
      "File": "AGENTS.md",
      "Repo": "REA.Bloodlines",
      "Path": "D:\\Projects\\MindAttic\\REA.Bloodlines\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.4,
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
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "SyntheticExchangeTheory",
      "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.QuickDuel",
      "Path": "D:\\Projects\\MindAttic\\Unity.QuickDuel\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
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
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.5555555555555556,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.QuickDuel",
      "Path": "D:\\Projects\\MindAttic\\Unity.QuickDuel\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.5555555555555556,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdItems",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdMoves",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdWeapons",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "TownWalker",
      "Path": "D:\\Projects\\MindAttic\\TownWalker\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Chain",
      "Path": "D:\\Projects\\MindAttic\\Unity.Chain\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.SpaceGod",
      "Path": "D:\\Projects\\MindAttic\\Unity.SpaceGod\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Web",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": ".prose/commands/unstuck.md",
      "Repo": "Unstuck",
      "Path": "D:\\Projects\\MindAttic\\Unstuck\\.prose/commands\\unstuck.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\unstuck.md",
      "CanonicalCount": 51,
      "Total": 52,
      "Variants": 2,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Eve",
      "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.GridGame",
      "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Incognito",
      "Path": "D:\\Projects\\MindAttic\\Unity.Incognito\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Overworld",
      "Path": "D:\\Projects\\MindAttic\\Unity.Overworld\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.ProjectVania",
      "Path": "D:\\Projects\\MindAttic\\Unity.ProjectVania\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md",
      "CanonicalCount": 31,
      "Total": 43,
      "Variants": 6,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.SpaceGod",
      "Path": "D:\\Projects\\MindAttic\\Unity.SpaceGod\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 64,
      "Variants": 24,
      "Similarity": 0.8333333333333334,
      "IsCustom": false
    }
  ],
  "Missing": [
    {
      "File": ".prose/commands/autopilot.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\autopilot.md"
    },
    {
      "File": ".prose/commands/bootstrap.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\bootstrap.md"
    },
    {
      "File": ".prose/commands/bughunt.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\bughunt.md"
    },
    {
      "File": ".prose/commands/commit.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\commit.md"
    },
    {
      "File": ".prose/commands/crossrepo.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\crossrepo.md"
    },
    {
      "File": ".prose/commands/deploy.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\deploy.md"
    },
    {
      "File": ".prose/commands/do.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\do.md"
    },
    {
      "File": ".prose/commands/progress.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\progress.md"
    },
    {
      "File": ".prose/commands/queue.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\queue.md"
    },
    {
      "File": ".prose/commands/quickload.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quickload.md"
    },
    {
      "File": ".prose/commands/quicksave.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoWebNav\\.prose/commands\\quicksave.md"
    },
    {
      "File": ".prose/commands/show.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\show.md"
    },
    {
      "File": ".prose/commands/throttle.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\throttle.md"
    },
    {
      "File": ".prose/commands/unstuck.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\unstuck.md"
    },
    {
      "File": ".prose/commands/unthrottle.md",
      "Repo": "Unity.QuickDuel",
      "HaveIt": 39,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\Automata\\.prose/commands\\unthrottle.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Automata",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "AutoWebNav",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Formicarium",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "GTFO.EZ",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "JobHunt",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "JoyceFit",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "KdpPublish",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Cryptography",
      "HaveIt": 31,
      "Eligible": 40,
      "ExamplePath": "D:\\Projects\\MindAttic\\ChiMesh\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Orchestrator",
      "HaveIt": 31,
      "Eligible": 40,
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
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "Unity.Eve",
          "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "Unity.GridGame",
          "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
        },
        {
          "Repo": "Unity.SpaceGod",
          "Path": "D:\\Projects\\MindAttic\\Unity.SpaceGod\\.claude\\hooks\\quickload-on-do.ps1",
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
          "Repo": "MindAttic.Tickets",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Tickets\\tools\\build-readme.ps1",
          "Lines": 13
        },
        {
          "Repo": "MindAttic.Vault",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Vault\\tools\\build-readme.ps1",
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
      "RepoCount": 16
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
          "Repo": "MindAttic.Tickets",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Tickets\\tools\\codex.ps1",
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
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\tools\\make_icons.py",
          "Lines": 153
        },
        {
          "Repo": "Unity.GridGame",
          "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\tools\\make_icons.py",
          "Lines": 182
        }
      ],
      "Identical": false,
      "Similarity": 0.6621621621621622,
      "SharedLines": 103,
      "SuggestedHome": "a shared Python package",
      "RepoCount": 2
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
          "Repo": "MindAttic.Agency",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Agency\\shared\\hooks\\quickload-on-do.ps1",
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
        },
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\.claude\\hooks\\quickload-on-do.ps1",
          "Lines": 58
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
          "Repo": "MindAttic.Agency",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Agency\\shared\\statusline.ps1",
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
          "Repo": "MindAttic.Tickets",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Tickets\\.claude\\hooks\\inject-digest.ps1",
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
          "Repo": "Unity.Multiplayer",
          "Path": "D:\\Projects\\MindAttic\\Unity.Multiplayer\\unity\\com.mindattic.unity.multiplayer\\Samples~\\GameTemplate\\Runtime\\Shapes.cs",
          "Lines": 13
        },
        {
          "Repo": "Unity.Ricochet",
          "Path": "D:\\Projects\\MindAttic\\Unity.Ricochet\\Assets\\Ricochet\\Runtime\\Shapes.cs",
          "Lines": 13
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 13,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
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
          "Repo": "MindAttic.Tickets",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Tickets\\src\\MindAttic.Tickets.Desktop\\ViewModels\\Mvvm.cs",
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
          "Repo": "Unity.Cursory",
          "Path": "D:\\Projects\\MindAttic\\Unity.Cursory\\tools\\unity.ps1",
          "Lines": 33
        },
        {
          "Repo": "Unity.Eve",
          "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\tools\\unity.ps1",
          "Lines": 33
        },
        {
          "Repo": "Unity.Hare",
          "Path": "D:\\Projects\\MindAttic\\Unity.Hare\\tools\\unity.ps1",
          "Lines": 32
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
      "RepoCount": 4
    },
    {
      "Files": [
        {
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\Assets\\Lab\\App\\Tween.cs",
          "Lines": 41
        },
        {
          "Repo": "Unity.GridGame",
          "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\Assets\\Gaiden\\App\\UI\\Tween.cs",
          "Lines": 49
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
          "Repo": "TownWalker",
          "Path": "D:\\Projects\\MindAttic\\TownWalker\\Assets\\Lab\\App\\Fonts.cs",
          "Lines": 23
        },
        {
          "Repo": "Unity.GridGame",
          "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\Assets\\Gaiden\\App\\Services\\Fonts.cs",
          "Lines": 44
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
          "Repo": "Unity.Multiplayer",
          "Path": "D:\\Projects\\MindAttic\\Unity.Multiplayer\\unity\\com.mindattic.unity.multiplayer\\Samples~\\GameTemplate\\Tests\\EditMode\\GameSetupTests.cs",
          "Lines": 25
        },
        {
          "Repo": "Unity.Ricochet",
          "Path": "D:\\Projects\\MindAttic\\Unity.Ricochet\\Assets\\Ricochet\\Tests\\EditMode\\GameSetupTests.cs",
          "Lines": 26
        }
      ],
      "Identical": false,
      "Similarity": 0.6,
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
INSERT INTO state (name, value) VALUES ('dashboard-repos', '{
  "Sort": "Open tickets"
}');
INSERT INTO state (name, value) VALUES ('limit-resumes', '{
  "70664": {
    "SessionId": "d63b8323-0214-4476-ab5a-1700c5edbd3d",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T18:22:14.8890877Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "72544": {
    "SessionId": "1ca9bd00-13a3-4fd3-b0f4-44f58e675c76",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T18:29:54.5311142Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "39852": {
    "SessionId": "98a40e2a-cc75-4838-86e0-d73a698227b1",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:38:23.8575044Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "71512": {
    "SessionId": "64fe7bd3-2e58-4ed4-9a5e-37c2828926fd",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:16:07.1829938Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "67856": {
    "SessionId": "51870e7d-1d97-4418-8b48-6097fb43ff7e",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:16:56.0173887Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "33880": {
    "SessionId": "ad7fbc23-533a-485c-adef-befaa1d47626",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:17:09.4960854Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "55968": {
    "SessionId": "004c0693-b78e-4101-8c5a-4d4bb6911a1b",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:17:22.4557253Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "41236": {
    "SessionId": "81152999-dc47-438d-8eaa-4b0922c3ece2",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:29:24.2537584Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "48372": {
    "SessionId": "fc6a58a5-b922-417b-8bb7-acf8d5519d89",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T19:47:31.516427Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "54244": {
    "SessionId": "9efa0c81-4cd0-45d6-b932-5b3c3231811c",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:20:22.9919659Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "62444": {
    "SessionId": "d9285b98-581c-48e0-a995-2068e04bece3",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:20:48.1393489Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "31024": {
    "SessionId": "fb98ff00-8878-4a14-b8d8-56b5088f0d17",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:21:28.3804977Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "15476": {
    "SessionId": "00cd01a1-e7b4-4a13-bc77-fe92abe508fa",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:22:00.9117276Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "53244": {
    "SessionId": "e4584685-15bf-4183-a6a9-236732441d16",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:23:07.9516816Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "2716": {
    "SessionId": "61d5cec2-4d9a-40fa-b33e-7db0ca79379e",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:23:36.3468787Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "77436": {
    "SessionId": "c3db9249-ec7f-45e3-8b14-46ad9d50d82e",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:24:24.0066311Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "44460": {
    "SessionId": "0e3d9912-af74-4f05-8c25-2603c82585c1",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:25:43.23526Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "5604": {
    "SessionId": "0f8387ba-fa84-4f28-b8cb-da38cf0a879d",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:26:07.7228479Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "74348": {
    "SessionId": "bf586b10-0d9f-4fb8-80a9-5d86be5699da",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:29:27.1874786Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "58872": {
    "SessionId": "f360cb66-220f-4164-924e-914c343fb2df",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:31:00.4400602Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "66352": {
    "SessionId": "e1115a1c-8787-44f6-9ae4-72c58c0d8c4a",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T20:42:37.7547455Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "16272": {
    "SessionId": "fc6a58a5-b922-417b-8bb7-acf8d5519d89",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:05:38.6718672Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "39660": {
    "SessionId": "ffb7b0e5-451a-4d53-beb9-5d3f0fec0530",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:14:11.4643048Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "47756": {
    "SessionId": "25e72d46-fc7c-4e32-937d-19b9d0f97fb0",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:15:02.9924859Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "56260": {
    "SessionId": "678ee873-9405-4739-8cd4-93adf229744f",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:17:32.4915793Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "32784": {
    "SessionId": "73cba3d1-c191-4012-a599-d842e95c6850",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:18:04.4782121Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "76836": {
    "SessionId": "abb70b4a-9c75-4b82-b7db-cf800c20851c",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:18:04.4782121Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "66672": {
    "SessionId": "abf80f04-4fe0-4b04-877d-2ccb1ccb214d",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:22:33.4777865Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "55304": {
    "SessionId": "b30482d7-48dc-4494-907b-78377c6448fe",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:22:33.4777865Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "49800": {
    "SessionId": "105d8ebd-4b74-44e9-82b6-85b7812d35fc",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:28:33.4399158Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "31496": {
    "SessionId": "55a5364c-f295-4141-9b0f-3e54ba479d68",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:43:18.9707673Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "25504": {
    "SessionId": "66101f61-5f6c-4ef1-b084-e8150cfc70f9",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:50:06.919043Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "77724": {
    "SessionId": "9ede4043-e9c3-4ac5-8b24-338347e2b74d",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:51:07.6499648Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "39260": {
    "SessionId": "aa51e672-2cba-4722-a561-d57d0bc86968",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:52:36.3381721Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "68672": {
    "SessionId": "1e6b9429-db7b-4a76-b10f-f9b9f02aa23c",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T22:54:57.9619245Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "43052": {
    "SessionId": "fe936aaf-26ea-444e-ac86-dde15736393e",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T23:35:18.5804685Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "38564": {
    "SessionId": "654a5400-904e-407f-8e50-e5a9693917d3",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-23T23:37:39.3771576Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "77184": {
    "SessionId": "1ff4f2d8-4c46-4b29-9f92-07ba59f9f33c",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:04:23.6067993Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "73436": {
    "SessionId": "ebfa0b7b-24aa-466f-a83a-3b4d3a262661",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:09:10.1831757Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "17544": {
    "SessionId": "66e645fc-c38f-40ca-9503-c928e0392c42",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:10:01.2334894Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "72360": {
    "SessionId": "89ca541c-cb35-4617-94be-5c87346cb247",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:10:01.2334894Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "59432": {
    "SessionId": "114901c6-c933-45df-9236-c6705327bb43",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:19:23.3134735Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "71940": {
    "SessionId": "2b906ad4-d851-4bc5-9655-1a1d3ebf3775",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:21:06.6533199Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "49512": {
    "SessionId": "d7f7f252-a846-4864-9e8e-389c22c4fa38",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:24:53.1842115Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "55948": {
    "SessionId": "ecc2aeed-abb0-4e6e-9860-28f3394e89eb",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:26:22.8416602Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "34812": {
    "SessionId": "6a4d0ff5-3183-4d0a-a3c7-f261ff488501",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:39:12.1288324Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "50156": {
    "SessionId": "3b88c9bd-7ae4-499d-8b9b-3dfa941c0372",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T00:57:24.1926317Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "72688": {
    "SessionId": "d66e3f88-f0ae-4730-b248-9a638cdeb609",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:03:57.7441148Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "77172": {
    "SessionId": "0b74d0f0-10bd-4fdc-a454-e28746779b99",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:53:14.4482022Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "64180": {
    "SessionId": "66709a05-e525-42dc-831d-a3dfb9f23234",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:07:38.6415615Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "12252": {
    "SessionId": "22ce4d90-c668-4388-aadf-585320eeff21",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:09:54.1979823Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "72540": {
    "SessionId": "9cadf186-6367-4156-bc32-0c0fd9c58358",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:10:39.2999538Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "12352": {
    "SessionId": "7e5f7210-b66f-4380-96e6-2d80b00f88b3",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:12:56.877964Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "40348": {
    "SessionId": "f3d1629b-17ef-424e-9dcd-c5976cbbb30f",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:13:25.0883332Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "27984": {
    "SessionId": "fa5f7470-6166-4c23-8566-2888d7039173",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:13:25.0883332Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "69848": {
    "SessionId": "3f9e21d6-7980-4f2f-979e-dcd6cc46c3a7",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:17:25.6309043Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "66960": {
    "SessionId": "1aef465b-8a47-4a66-99e7-ad07c29a8e1a",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:18:17.09504Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "34892": {
    "SessionId": "3f18482e-ae5a-4b91-8dad-20355d6415c6",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:36:51.0262804Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "31292": {
    "SessionId": "60a2c669-6d1e-4de2-b1d8-1660f771603e",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:43:18.3081145Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "58728": {
    "SessionId": "04f0f32f-0901-4b8d-82ea-62f2325b88b7",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T01:54:37.4978798Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "65628": {
    "SessionId": "0548d2be-05dd-4c9f-8857-f2e161a4fd28",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T02:04:16.6828883Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "74552": {
    "SessionId": "c3ee7854-983c-441e-9dae-77260b23a6a6",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T02:06:53.8936099Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "46704": {
    "SessionId": "920f73e2-defc-4a55-b788-dcf01eab5395",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T03:27:46.7086203Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "75040": {
    "SessionId": "b88d48be-6334-4056-84cb-f601c2296e73",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T03:40:43.8305305Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "35616": {
    "SessionId": "8c6394ee-f1b8-4b85-8d7a-50a4527056f9",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T03:41:37.350248Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "74396": {
    "SessionId": "85d0f632-a7ee-466f-9566-4b788f68fc88",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T03:49:28.5951645Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "22844": {
    "SessionId": "9976eacd-149e-4ad5-a16b-b31ecc8eb4cc",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T03:49:41.1815026Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  },
  "11408": {
    "SessionId": "3355791e-fb1c-436b-9d8e-7fb6aeda09fd",
    "Window": "weekly",
    "UntilUtc": "2026-10-27T22:00:00Z",
    "StoppedUtc": "2026-10-24T03:52:03.18421Z",
    "DueUtc": "2026-10-27T22:02:00Z"
  }
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
  "71ffc829-973e-41e6-bfe0-95e4e7759eff@2026-10-20T20:30:00Z": "2026-10-20T20:30:00Z",
  "0108d19d-53e2-4bdc-88fc-d7459918ee0a@2026-10-21T06:30:00Z": "2026-10-21T06:30:00Z",
  "2b46e72b-03e8-439f-8047-9ef30321cc23@2026-10-21T06:30:00Z": "2026-10-21T06:30:00Z",
  "56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c@2026-10-21T18:40:00Z": "2026-10-21T18:40:00Z",
  "85827f8f-18e4-4add-b8f6-893ccf7bc987@2026-10-21T18:40:00Z": "2026-10-21T18:40:00Z",
  "2a2ff560-06bf-4989-8ce4-23189afbaada@2026-10-21T23:40:00Z": "2026-10-21T23:40:00Z",
  "176d3352-9d94-465c-8b51-9651d77a43ed@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "2a2ff560-06bf-4989-8ce4-23189afbaada@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "84549ed4-ac32-49be-80c4-5a4af4a90f8a@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "85827f8f-18e4-4add-b8f6-893ccf7bc987@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "a988c566-86ca-491b-9553-adf19cca46c6@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "476f6f2a-73c7-4e9a-836e-969d74acef9d@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "8cfb5fdd-a868-49e0-bb37-fb493a29aefa@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "d4f0b418-5004-492e-960f-8fda51bf4ea3@2026-10-22T04:40:00Z": "2026-10-22T04:40:00Z",
  "2230fb99-d7f9-4acd-a1bb-06cfba1568bb@2026-10-22T09:40:00Z": "2026-10-22T09:40:00Z",
  "476f6f2a-73c7-4e9a-836e-969d74acef9d@2026-10-22T09:40:00Z": "2026-10-22T09:40:00Z",
  "56bfedfc-f1f6-4eaf-92cf-a93bd8fa5a5c@2026-10-22T09:40:00Z": "2026-10-22T09:40:00Z",
  "85827f8f-18e4-4add-b8f6-893ccf7bc987@2026-10-22T09:40:00Z": "2026-10-22T09:40:00Z",
  "8cfb5fdd-a868-49e0-bb37-fb493a29aefa@2026-10-22T09:40:00Z": "2026-10-22T09:40:00Z",
  "66d3da02-ec62-4b6f-a7bb-44cc8b7b0c3f@2026-10-22T21:20:00Z": "2026-10-22T21:20:00Z",
  "41df9bb7-c213-4b78-b6a6-4eec9591facb@2026-10-23T00:20:00Z": "2026-10-23T00:20:00Z",
  "703a0095-0c8e-4b3f-8af9-9c845b243c86@2026-10-23T00:20:00Z": "2026-10-23T00:20:00Z",
  "d3469031-4b42-40c5-a2e3-58a88fb2b06e@2026-10-23T00:20:00Z": "2026-10-23T00:20:00Z",
  "4ef11499-eaa9-4435-bc12-c068b0d0ba06@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "555266d3-de95-4a86-beb2-152b31adedd1@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "69dd2e90-8537-4f86-be6d-004afc057e14@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "6c96be27-858b-45cc-ba27-7987c2e39ae5@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "8a8e89ea-45f7-4548-95f2-ba1c125840d7@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "d3469031-4b42-40c5-a2e3-58a88fb2b06e@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "dfd6fc55-5321-493f-bbc2-601cca3fa859@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "e84a8dcc-2f07-43b6-a4f5-c7a148ec4cff@2026-10-23T05:20:00Z": "2026-10-23T05:20:00Z",
  "4ef11499-eaa9-4435-bc12-c068b0d0ba06@2026-10-23T10:20:00Z": "2026-10-23T10:20:00Z",
  "69dd2e90-8537-4f86-be6d-004afc057e14@2026-10-23T10:20:00Z": "2026-10-23T10:20:00Z",
  "8a8e89ea-45f7-4548-95f2-ba1c125840d7@2026-10-23T10:20:00Z": "2026-10-23T10:20:00Z",
  "c57d7bd3-fe9a-4798-b92d-5eb892c5c7f2@2026-10-23T10:20:00Z": "2026-10-23T10:20:00Z"
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
INSERT INTO state (name, value) VALUES ('mainline-tests', '{
  "Automata": {
    "Tested": "c1b3be6d1ef3e82d0a4123ec23b0688c73cd95ec",
    "Green": "c1b3be6d1ef3e82d0a4123ec23b0688c73cd95ec"
  },
  "AutoWebNav": {
    "Tested": "ff3f33c92ea4a51b509e9cdfdef3915cee91ff6c"
  },
  "Cursory": {
    "Tested": "534d7658ff1d9d719ce00099c6af3c335da0a8f9"
  },
  "Formicarium": {
    "Tested": "1c0cea2b656dd86b99baa2722c369a7945ce8c11"
  },
  "Gadfly": {
    "Tested": "44f0949c5fe8b3e15ca1eaf2b0163c98d7ee93f4"
  },
  "IdiotProof": {
    "Tested": "8444db9ff9c940df9c04336c885eb283962d4b9e"
  },
  "JobHunt": {
    "Tested": "82ca51067bca7dbab5c78171de17607a4ad9245b",
    "Green": "82ca51067bca7dbab5c78171de17607a4ad9245b"
  },
  "MediaButler": {
    "Tested": "518f409acec6f232e534db8589196d7ef42735dd"
  },
  "MindAttic.Authentication": {
    "Tested": "8f54822a67d6109568f734994a3c5992998a9882"
  },
  "MindAttic.Browser": {
    "Tested": "c004020eea0bc2f86b64f08dd09c7592ec5d311e",
    "Green": "c004020eea0bc2f86b64f08dd09c7592ec5d311e"
  },
  "MindAttic.Capture": {
    "Tested": "fc72692ec6bd86d7d8c48b050dc160f9c0e3d3ba",
    "Green": "fc72692ec6bd86d7d8c48b050dc160f9c0e3d3ba"
  },
  "MindAttic.Components": {
    "Tested": "ed635a7d49ed573f515f0241742012ecb3af3a85",
    "Green": "ed635a7d49ed573f515f0241742012ecb3af3a85"
  },
  "MindAttic.Deploy": {
    "Tested": "69371ac626ebb16861055e557f410ffc15b92680"
  },
  "MindAttic.Export": {
    "Tested": "061cd3c9188d8940cea2613187da951d5c08191c"
  },
  "MindAttic.Helpers": {
    "Tested": "4bd229f854178b6ed1f0dd1951e6fb3225a4ba9a"
  },
  "MindAttic.Ideas": {
    "Tested": "48247a0eb29f9608042de9268356673bde09ac3a"
  },
  "MindAttic.Launcher": {
    "Tested": "f74493668a3bd8a728455406d881d7408d6a24af",
    "Green": "f74493668a3bd8a728455406d881d7408d6a24af"
  },
  "MindAttic.Legion": {
    "Tested": "0f63adaaae25e3a5fc8498c9973ab151ca31bd96"
  },
  "MindAttic.Log": {
    "Tested": "4358effc9d75fabf5c67ad4e742be0e490be265d"
  },
  "MindAttic.Media": {
    "Tested": "a57a30142679f3ed216b7bb6a585534536b6391c"
  },
  "MindAttic.Orchestrator": {
    "Tested": "e6c758f11d42f47df91db444fba8fa1695e44fc4",
    "Green": "2c4f9bdd7534944aa4bccc98fd872ae613910efe"
  },
  "MindAttic.Psst": {
    "Tested": "4a8dd88cf5df31911bcff9ec0b040a5008399a02"
  },
  "MindAttic.Telemetry": {
    "Tested": "7332476b16161496300b69d2c8838a9cb55f38e7"
  },
  "MindAttic.Tickets": {
    "Tested": "2abf6b49c0855626da63a8237e835b2c2985667b",
    "Green": "2abf6b49c0855626da63a8237e835b2c2985667b"
  },
  "MindAttic.Vault": {
    "Tested": "ac2e2fd848071f2cd0db1f8a77f0bb3dad28d3d8",
    "Green": "ac2e2fd848071f2cd0db1f8a77f0bb3dad28d3d8"
  },
  "OpenCredentials": {
    "Tested": "8b046123b94fb1dfcd75dedeff9575e6b8081a70"
  },
  "ProcessLogger": {
    "Tested": "bddb249caed282380de8d1cbee39d252455e7bca"
  },
  "TaxRateCollector": {
    "Tested": "8030495c6a9a623afb6e5517365d5931c21fe17f"
  },
  "ThinkTank": {
    "Tested": "2e159a9b7f7b56aa9741cc25bedd1c4dac95ebb4"
  },
  "Tutor": {
    "Tested": "d21e2389682a5f31fbaf4b89538e304cb49da3af"
  },
  "Unity.GridGame": {
    "Tested": "0223132d1fd651281201d57401151b833fd53fa8"
  },
  "Unity.Cursory": {
    "Tested": "acedc8cfce90dfa20e8bb0cd99b23ae36acd8c1c",
    "Green": "acedc8cfce90dfa20e8bb0cd99b23ae36acd8c1c"
  },
  "REA.Bloodlines": {
    "Tested": "11c7e15c9ab82800a7034d22c3a7a944f8485826",
    "Green": "11c7e15c9ab82800a7034d22c3a7a944f8485826"
  },
  "ChiMesh": {
    "Tested": "e255217c5f32a7c2bef79ef86c14a2fb9a0de05f"
  },
  "Claudia": {
    "Tested": "34d00bfb963e29dc9af2177625726035bf9aae2b"
  },
  "GTFO.CrashTestDummies": {
    "Tested": "61ab9195203c7cdcebc2791c3d4fa7d31cbc8717"
  },
  "GTFO.EZ": {
    "Tested": "88ab9ea5994811c262ec495a576f05c02c057da6"
  },
  "GTFO.EZ.AbsurdItems": {
    "Tested": "47268f99927740a5781d8299c21fcded059b916d"
  },
  "GTFO.EZ.AbsurdMoves": {
    "Tested": "1403b6b9f5a97fbae19d944c6cb7b8879c050865"
  },
  "GTFO.EZ.AbsurdWeapons": {
    "Tested": "edd5504a135aadaf8214d263db0932f7e203c114"
  },
  "GTFO.EZ.Shared": {
    "Tested": "7db86f4fab47bdd578204676dd945ede77c6c19e"
  },
  "GTFO.SkipDrop": {
    "Tested": "1ae4a067bfd885aac5206f041290b5272e55baf6"
  },
  "JellyFinPoster": {
    "Tested": "0c9f87faa073daafd27522ee7693346fbe99245f"
  },
  "JoyceFit": {
    "Tested": "de64c3bca2f1e545dfaae8b72d3f974aa0fd4713"
  },
  "KdpPublish": {
    "Tested": "75724978180959fdbc9f470422b61cf46dde4daa"
  },
  "MindAttic.Agency": {
    "Tested": "c829e9437d167c68441071f75d8067baa043a9c7"
  },
  "MindAttic.Cryptography": {
    "Tested": "6436de35af93e61a48725be15dcf0e736a12b9c4"
  },
  "MindAttic.Scheduler": {
    "Tested": "caaf68692f8a1feeffd0dc18a63086aaee33a532"
  },
  "MindAttic.Web": {
    "Tested": "db817ef9047d74799520f8b80d5bbf56841eb13c"
  },
  "Prose": {
    "Tested": "e5a4cf73a92608ce826b29365c196024b0cee86b"
  },
  "SyntheticExchangeTheory": {
    "Tested": "3d4595bdbe439878fdc544d9fca8d821b3b7740c"
  },
  "TownWalker": {
    "Tested": "1cc20f94bf87b83ee5a4753c799aea4e0ec79083"
  },
  "Unity.Eve": {
    "Tested": "74657741e5146ddfd3eed4727a091df8031b680e"
  },
  "Unity.Multiplayer": {
    "Tested": "52e2f057a3d847c250c5a320cb47842e621c5ce9"
  },
  "Unity.Shared": {
    "Tested": "612f448ff95185047c9a6ca2ff9d5fb78abb0c98"
  },
  "Unity.SpaceGod": {
    "Tested": "54c2751bdd536d66bddc575dc09f69098a057e32"
  },
  "UnityRonin": {
    "Tested": "b5fc5f1566f00fa0d204a9252b7774e2873f9255"
  },
  "Unstuck": {
    "Tested": "0afda1a8288caf6d651a7b6c28ad5c1b0422a3a7"
  },
  "Unity.Chain": {
    "Tested": "66a386b6abf20416ee7534e664a0f68b7ca82657"
  },
  "Unity.Hare": {
    "Tested": "e5c5a5980e4b488773a871efebb11ebd0dc7dae1"
  },
  "Unity.Incognito": {
    "Tested": "2dd2189551fd40df11064e5480c10acb81eac279"
  },
  "Unity.QuickDuel": {
    "Tested": "3741cf1bcac655aa6703084674f22fbcb931616c",
    "Green": "3741cf1bcac655aa6703084674f22fbcb931616c"
  },
  "Unity.Ricochet": {
    "Tested": "f6afda509eb23696ebb872500d3332997c2055e4",
    "Green": "f6afda509eb23696ebb872500d3332997c2055e4"
  },
  "Unity.SongOfDawn": {
    "Tested": "cf0ba56cfe8bbe4f709056e977efe83667c974c8"
  },
  "Unity.Overworld": {
    "Tested": "07c07a9b74309e29e7be5c39d34a38dd2d7c9c88"
  },
  "Unity.ProjectVania": {
    "Tested": "7dc97f6bb5c85e667428da61a0ba70552bc58d1b"
  }
}');
INSERT INTO state (name, value) VALUES ('orphaned-tickets', '[
  {
    "TicketId": "M.CAPT-T12",
    "Repo": "MindAttic.Capture",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\MindAttic.Capture\\M.CAPT-T12",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T03:59:43.8422976Z",
    "RedispatchedUtc": "2026-10-24T03:59:43.0321025Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "PRS-T133",
    "Repo": "Prose",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Prose\\PRS-T133",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T03:59:44.9448021Z",
    "RedispatchedUtc": "2026-10-24T03:59:43.0321025Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "U.EVE-T19",
    "Repo": "Unity.Eve",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.Eve\\U.EVE-T19",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T04:10:27.8573412Z",
    "RedispatchedUtc": "2026-10-24T04:10:26.9617563Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "UM-T74",
    "Repo": "Unity.Multiplayer",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.Multiplayer\\UM-T74",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-23T05:21:23.9281867Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 61948,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "UC-T34",
    "Repo": "Unity.Cursory",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.Cursory\\UC-T34",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-23T05:21:17.2034141Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 32696,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "GG-T54",
    "Repo": "Unity.GridGame",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.GridGame\\GG-T54",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T04:13:52.3179455Z",
    "RedispatchedUtc": "2026-10-24T04:13:46.9847265Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [
      "ssets/Gaiden/Meta/CampRoutine.cs",
      "ssets/Gaiden/Meta/MetaServices.cs",
      "ssets/Gaiden/Meta/TestLoop.cs",
      "ests/Core.Tests/MetaCommitBuildTests.cs",
      "ests/Core.Tests/SkillTreeTests.cs",
      "ests/Core.Tests/TestModeTests.cs",
      "ools/harness.py"
    ],
    "Open": true
  },
  {
    "TicketId": "GG-T53",
    "Repo": "Unity.GridGame",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.GridGame\\GG-T53",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T04:13:53.251735Z",
    "RedispatchedUtc": "2026-10-24T04:13:46.9847265Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [
      "ssets/Gaiden/App/Automation/StartScreens.cs",
      "ssets/Gaiden/App/Screens/MenuScreens.cs",
      "ssets/Gaiden/Meta/MetaServices.cs",
      "ssets/Gaiden/Meta/SaveData.cs",
      "ocs/BIBLE.md",
      "ools/harness.py",
      "Assets/Gaiden/App/Screens/SkillTreeScreen.cs",
      "Assets/Gaiden/App/Screens/SkillTreeScreen.cs.meta",
      "Tests/Core.Tests/TalentsTests.cs"
    ],
    "Open": true
  },
  {
    "TicketId": "RB-T152",
    "Repo": "REA.Bloodlines",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\REA.Bloodlines\\RB-T152",
    "FirstSeenUtc": "2026-10-23T22:52:59.3465752Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T03:56:38.8624778Z",
    "RedispatchedUtc": "2026-10-24T03:56:19.477948Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "RB-T110",
    "Repo": "REA.Bloodlines",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\REA.Bloodlines\\RB-T110",
    "FirstSeenUtc": "2026-10-24T01:07:38.9627003Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T12:36:03.4614359Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 34068,
    "Ahead": 0,
    "Uncommitted": [
      "rc/Bloodlines.Data/Maps/MapEntityRules.cs",
      "rc/Bloodlines.Sbox/.gitignore",
      "rc/Bloodlines.Sbox/Code/Combat/ThrownGrenade.cs",
      "rc/Bloodlines.Sbox/Code/Runtime/BloodlinesEntity.cs",
      "rc/Bloodlines.Sbox/Code/Runtime/MapSpawner.cs",
      "rc/Bloodlines.Sbox/Code/Runtime/PointExplosion.cs",
      "ests/Bloodlines.Data.Tests/MapEntitiesTests.cs",
      "ools/sync-sbox.ps1",
      "src/Bloodlines.Data/Particles/",
      "src/Bloodlines.Sbox/Assets/shaders/bloodlines_particle.shader",
      "src/Bloodlines.Sbox/Code/Runtime/Effects.cs",
      "src/Bloodlines.Sbox/Code/Runtime/Particles.cs"
    ],
    "Open": true
  },
  {
    "TicketId": "U.SPC-T486",
    "Repo": "Unity.SpaceGod",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.SpaceGod\\U.SPC-T486",
    "FirstSeenUtc": "2026-10-24T02:02:17.821576Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T01:06:04.8822663Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 73436,
    "Ahead": 2,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "U.SPC-T491",
    "Repo": "Unity.SpaceGod",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.SpaceGod\\U.SPC-T491",
    "FirstSeenUtc": "2026-10-24T02:02:17.821576Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T01:13:42.7077773Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 73436,
    "Ahead": 0,
    "Uncommitted": [
      "Assets/UnitySpace/Core/Combat/Battle.cs",
      "Assets/UnitySpace/Core/Combat/Broadsides.cs",
      "Assets/UnitySpace/Core/Combat/CombatSpecs.cs",
      "Assets/UnitySpace/Core/Combat/Radiators.cs",
      "Assets/UnitySpace/Core/Combat/Radiators.cs.meta",
      "Assets/UnitySpace/Core/ControlsHelp.cs",
      "Assets/UnitySpace/Runtime/Input/InputState.cs",
      "Assets/UnitySpace/Runtime/Rendering/ShipMeshFactory.cs",
      "Assets/UnitySpace/Runtime/UI/Hud.cs",
      "Assets/UnitySpace/Runtime/UnitySpaceApp.cs",
      "README.md",
      "Tests/Core.Tests/RadiatorTests.cs"
    ],
    "Open": true
  },
  {
    "TicketId": "U.SPC-T498",
    "Repo": "Unity.SpaceGod",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.SpaceGod\\U.SPC-T498",
    "FirstSeenUtc": "2026-10-24T02:02:17.821576Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T01:15:39.3857618Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 73436,
    "Ahead": 0,
    "Uncommitted": [
      "ssets/UnitySpace/Core/Economy/BeltMining.cs",
      "ssets/UnitySpace/Core/Fleet.cs",
      "ssets/UnitySpace/Core/Hyperlanes.cs",
      "ssets/UnitySpace/Core/ShipLod.cs",
      "ssets/UnitySpace/Core/Ships.cs",
      "ssets/UnitySpace/Core/World.Mining.cs",
      "ssets/UnitySpace/Core/World.cs",
      "ssets/UnitySpace/Runtime/Rendering/ShipMeshFactory.cs",
      "ssets/UnitySpace/Runtime/UnitySpaceApp.Streaming.cs",
      "ssets/UnitySpace/Runtime/UnitySpaceApp.cs",
      "ssets/UnitySpace/Runtime/Views/ShipView.cs",
      "EADME.md"
    ],
    "Open": true
  },
  {
    "TicketId": "M.LNCH-T23",
    "Repo": "MindAttic.Launcher",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\MindAttic.Launcher\\M.LNCH-T23",
    "FirstSeenUtc": "2026-10-24T02:02:35.752508Z",
    "State": "orphaned",
    "Deaths": 2,
    "Redispatches": 1,
    "DeadStartedUtc": "2026-10-24T03:56:37.1332515Z",
    "RedispatchedUtc": "2026-10-24T03:56:19.477948Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "M.ORCH-T774",
    "Repo": "MindAttic.Orchestrator",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\MindAttic.Orchestrator\\M.ORCH-T774",
    "FirstSeenUtc": "2026-10-24T10:20:31.5872083Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T05:24:24.9122382Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 22844,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "MAT-T49",
    "Repo": "MindAttic.Agency",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\MindAttic.Agency\\MAT-T49",
    "FirstSeenUtc": "2026-10-24T10:37:40.9018422Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T06:38:31.351098Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 11408,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "UH-T41",
    "Repo": "Unity.Hare",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.Hare\\UH-T41",
    "FirstSeenUtc": "2026-10-24T11:18:23.1263815Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T10:53:41.786846Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 46704,
    "Ahead": 0,
    "Uncommitted": [
      "ssets/Unity.Hare/Core/Enemies/EnemyComposition.cs",
      "ssets/Unity.Hare/Core/Enemies/EnemyStats.cs",
      "ssets/Unity.Hare/Core/Player/FlightTuning.cs",
      "ssets/Unity.Hare/Scripts/Enemies/EnemyEmitter.cs",
      "ssets/Unity.Hare/Scripts/Enemies/WormholeEmitter.cs",
      "ssets/Unity.Hare/Scripts/Player/PlayerHealth.cs",
      "ssets/Unity.Hare/Scripts/Player/PlayerSeat.cs",
      "ssets/Unity.Hare/Scripts/Player/PlayerShipController.cs",
      "ssets/Unity.Hare/Scripts/Weapons/AirWeaponLoadout.cs",
      "ssets/Unity.Hare/Scripts/Weapons/Flamethrower.cs",
      "ssets/Unity.Hare/Scripts/Weapons/LaserGun.cs",
      "ssets/Unity.Hare/Scripts/Weapons/PerchedWeapon.cs"
    ],
    "Open": true
  },
  {
    "TicketId": "UR-T36",
    "Repo": "Unity.Ricochet",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.Ricochet\\UR-T36",
    "FirstSeenUtc": "2026-10-24T11:18:23.1263815Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T11:07:26.6291281Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 46704,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "U.OVW-T7",
    "Repo": "Unity.Overworld",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\Unity.Overworld\\U.OVW-T7",
    "FirstSeenUtc": "2026-10-24T15:28:07.6258926Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-24T11:36:53.9165725Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 72228,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
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
INSERT INTO state (name, value) VALUES ('rebalance-moves', '[
  {
    "AtUtc": "2026-10-24T13:33:57.2055376Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 82% of this window and 82% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T13:36:11.2024896Z",
    "Text": "honey stopped using Claude (20 minutes with nothing new): honey\u0027s CLIs are back to 98% of this window."
  },
  {
    "AtUtc": "2026-10-24T14:16:29.769351Z",
    "Text": "honey is using Claude (the meter rose 10 points in 29 min; the CLIs here spent 6 of them): honey\u0027s CLIs are held to 88% of this window and 84% of the week, and back to 98% about 20 minutes after honey stops."
  },
  {
    "AtUtc": "2026-10-24T14:37:13.2268483Z",
    "Text": "honey stopped using Claude (20 minutes with nothing new): honey\u0027s CLIs are back to 98% of this window."
  },
  {
    "AtUtc": "2026-10-24T14:38:25.5234408Z",
    "Text": "honey is using Claude (the meter rose 5 points in 28 min; the CLIs here spent 2 of them): honey\u0027s CLIs are held to 92% of this window and 86% of the week, and back to 98% about 20 minutes after honey stops."
  },
  {
    "AtUtc": "2026-10-24T14:58:15.5768701Z",
    "Text": "honey stopped using Claude (20 minutes with nothing new): honey\u0027s CLIs are back to 98% of this window."
  },
  {
    "AtUtc": "2026-10-24T15:38:50.7733461Z",
    "Text": "honey is using Claude (the meter rose 13 points in 17 min; the CLIs here spent 8 of them): honey\u0027s CLIs are held to 27% of this window and 78% of the week, and back to 98% about 20 minutes after honey stops."
  },
  {
    "AtUtc": "2026-10-24T15:41:29.4466597Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 48% of this window and 87% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:42:38.6762472Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 46% of this window and 75% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:43:39.0257225Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 45% of this window and 77% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:45:02.9386552Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 43% of this window and 79% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:47:25.2331583Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 35% of this window and 82% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:49:39.7450836Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 35% of this window and 74% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:51:47.3160994Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 36% of this window and 78% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:55:52.9823529Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 36% of this window and 81% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T15:58:13.9059722Z",
    "Text": "honey stopped using Claude (20 minutes with nothing new): honey\u0027s CLIs are back to 98% of this window."
  },
  {
    "AtUtc": "2026-10-24T15:59:30.0315404Z",
    "Text": "honey is using Claude (the meter rose 23 points in 27 min; the CLIs here spent 16 of them): honey\u0027s CLIs are held to 34% of this window and 74% of the week, and back to 98% about 20 minutes after honey stops."
  },
  {
    "AtUtc": "2026-10-24T16:00:36.2432212Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 33% of this window and 75% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T16:12:44.6733769Z",
    "Text": "at honey\u0027s rate of use the window would run out above the last ceiling: honey\u0027s CLIs are held to 60% of this window and 79% of the week now, leaving room for honey\u0027s projected use plus its headroom."
  },
  {
    "AtUtc": "2026-10-24T16:18:33.0548867Z",
    "Text": "honey stopped using Claude (20 minutes with nothing new): honey\u0027s CLIs are back to 98% of this window."
  }
]');
INSERT INTO state (name, value) VALUES ('usage-cutoff-alerts', '{
  "Alerted": {
    "pdftransferjr@mindattic.com|week": "2026-10-25T12:00:00Z",
    "ohleo199@mindattic.com|week": "2026-10-27T22:00:00Z",
    "honey@mindattic.com|5h": "2026-10-24T03:00:00Z",
    "ryandebraal@mindattic.com|5h": "2026-10-24T05:20:00Z"
  }
}');
INSERT INTO state (name, value) VALUES ('usage-cutoffs', '{
  "Cutoffs": [
    {
      "Email": "honey@mindattic.com",
      "Name": "Honey",
      "WeeklyPercent": 95,
      "FiveHourPercent": 98
    },
    {
      "Email": "ohleo199@mindattic.com",
      "Name": "Erin",
      "WeeklyPercent": 95,
      "FiveHourPercent": 98
    },
    {
      "Email": "pdftransferjr@mindattic.com",
      "Name": "Jordan",
      "WeeklyPercent": 95,
      "FiveHourPercent": 98
    },
    {
      "Email": "ryandebraal@mindattic.com",
      "Name": "Ryan",
      "WeeklyPercent": 95,
      "FiveHourPercent": 98
    },
    {
      "Email": "shampoo1232@mindattic.com",
      "Name": "Amy",
      "WeeklyPercent": 95,
      "FiveHourPercent": 98
    }
  ]
}');
