-- MindAttic.Orchestrator database dump (written with every change; the .db file is the store).

-- accounts
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', 'ohleo199@mindattic.com', 'MindAttic', 'Team · Max 5x', 0, 1793332800, 11, 1793743200, 1793315177, 1792686357.102392);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', 'ryandebraal@mindattic.com', 'MindAttic', 'Team · Max 5x', 0, 1792609200, 100, 1792764000, 1792591343, 1792591305.195613);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', 'shampoo1232@mindattic.com', 'MindAttic', 'Team · Max 5x', 51, 1792600200, 96, 1792702800, 1792589089, 1792464053.5616076);
INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at) VALUES ('8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', 'pdftransferjr@mindattic.com', 'MindAttic', 'Team · Max 5x', 81, 1792688400, 93, 1792929600, 1792685787, 1792670412.137807);

-- members
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('honey@mindattic.com', 'Honey', 'admin', 'premium', 'active', 1792771886.8054008, 1792771886.8054008);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('ohleo199@mindattic.com', 'Erin', 'admin', 'premium', 'active', 1792771886.302, 1793370770.16842);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('pdftransferjr@mindattic.com', 'Jordan', 'admin', 'premium', 'active', 1792771887.249, 1793417692.938722);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('ryandebraal@mindattic.com', 'Ryan', 'primary-owner', 'premium', 'active', 1792771885.463, 1792840279.0027995);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('shampoo1232@mindattic.com', 'Amy', 'admin', 'premium', 'active', 1792771885.882, 1793417692.938722);
INSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('waffle@mindattic.com', 'Waffle', 'admin', 'premium', 'active', 1792866937.8856213, 1792866937.8856213);

-- usage_samples
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
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T01:45:20Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T01:49:23Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T01:53:38Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T01:57:47Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:02:08Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:06:34Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:11:05Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:15:09Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:20:11Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:25:24Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:30:45Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:36:05Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:41:17Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:46:24Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:51:51Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T02:57:16Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:12:49Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:18:08Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:23:29Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:29:13Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:34:37Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:39:40Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:45:38Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:51:19Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T03:56:32Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:01:53Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:07:23Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:12:48Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:18:51Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:27:50Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:35:47Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:41:41Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:47:20Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:53:07Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T04:58:34Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:04:03Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:09:57Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:15:10Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:20:14Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:29:37Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:34:55Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:40:32Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:45:51Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:51:07Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T05:56:12Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:02:04Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:07:11Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:12:15Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:17:17Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:25:16Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:30:29Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-26T06:35:42Z', 0, '2026-10-26T06:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T14:59:12Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:03:22Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:07:32Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:12:12Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:16:27Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:21:08Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:25:39Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:31:05Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:36:32Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:41:37Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:46:51Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:52:07Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T15:57:22Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:02:26Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:07:45Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:12:49Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:18:25Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:23:55Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:29:56Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:35:29Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:40:45Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:46:08Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:51:33Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T16:57:10Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:02:34Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:07:55Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:13:30Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:18:39Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:23:59Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:29:08Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:34:23Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:39:42Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:45:11Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:50:25Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T17:55:37Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:00:54Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:06:11Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:11:42Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:17:36Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:22:45Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:30:28Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:37:41Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:44:18Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:49:41Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T18:54:52Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:03:34Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:09:03Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:14:07Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:19:07Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:24:13Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:29:17Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:34:27Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:39:33Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:44:43Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T19:49:50Z', 0, '2026-10-27T19:50:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T21:50:50Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T21:55:09Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T21:59:29Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:03:48Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:07:54Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:12:15Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:16:31Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:20:43Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:25:54Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:31:09Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:36:40Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:42:02Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:47:12Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:52:32Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T22:57:35Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:02:39Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:07:42Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:12:53Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:18:05Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:23:10Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:28:18Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:33:39Z', 0, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:38:46Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:42:48Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:46:54Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:51:07Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:55:19Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-27T23:59:24Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:03:42Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:07:47Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:12:49Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:18:05Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:23:16Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:28:34Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:33:41Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:38:52Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:44:08Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:49:09Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:54:15Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T00:59:28Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:04:46Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:10:00Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:15:06Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:20:12Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:25:24Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:30:35Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:35:45Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:41:00Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:46:03Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:51:20Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T01:56:32Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:01:47Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:06:51Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:11:54Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:16:57Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:22:03Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:32:23Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T02:37:24Z', 1, '2026-10-28T02:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:09:28Z', 0, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:13:42Z', 0, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:17:53Z', 0, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:22:02Z', 0, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:26:13Z', 1, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:30:19Z', 2, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:34:30Z', 3, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:38:40Z', 5, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:42:50Z', 7, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:46:53Z', 8, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:50:54Z', 10, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:55:08Z', 10, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T14:59:27Z', 11, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:03:39Z', 12, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:07:59Z', 13, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:12:07Z', 15, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:16:12Z', 17, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:20:21Z', 18, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:24:25Z', 19, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:28:31Z', 20, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:32:39Z', 21, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:36:44Z', 22, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:41:02Z', 24, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:45:11Z', 26, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:49:13Z', 27, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:53:21Z', 28, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T15:57:25Z', 30, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:01:37Z', 31, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:05:48Z', 32, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:10:08Z', 32, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:14:25Z', 33, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:18:37Z', 34, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:22:41Z', 35, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:26:51Z', 35, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:31:02Z', 39, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:35:05Z', 42, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:39:08Z', 44, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:43:21Z', 46, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:47:38Z', 47, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:51:59Z', 49, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T16:56:25Z', 50, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:00:44Z', 50, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:04:50Z', 51, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:09:14Z', 51, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:13:28Z', 53, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:17:38Z', 56, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:21:49Z', 58, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:26:09Z', 61, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:30:41Z', 62, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:34:59Z', 63, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:39:02Z', 64, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:43:08Z', 64, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:47:18Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:51:20Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:55:31Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T17:59:35Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:03:41Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:07:55Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:12:09Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:16:17Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:21:25Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:26:28Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:31:42Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:36:54Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:41:56Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:47:12Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:52:22Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-28T18:57:36Z', 65, '2026-10-28T19:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T14:50:45Z', 0, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T14:54:54Z', 0, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T14:58:58Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:03:14Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:07:19Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:11:28Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:15:41Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:19:51Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:23:57Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:27:58Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:33:12Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:38:28Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:43:43Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:48:54Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:54:10Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T15:59:20Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:04:33Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:09:36Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:14:40Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:19:47Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:25:01Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:30:05Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:35:18Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:40:35Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:45:39Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:50:45Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T16:56:03Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:01:19Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:06:26Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:11:31Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:16:36Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:21:40Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:26:57Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:32:12Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:37:13Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:42:24Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:47:43Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:52:59Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T17:58:07Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:03:08Z', 1, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:08:15Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:12:40Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:16:56Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:21:14Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:25:26Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:29:35Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:33:45Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:37:48Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:42:49Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:48:03Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:53:30Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T18:58:36Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:03:42Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:08:46Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:14:02Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:19:12Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:24:22Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:29:43Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:34:55Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T19:39:55Z', 2, '2026-10-29T19:40:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:06:17Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:10:20Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:14:36Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:18:47Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:23:07Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:27:22Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:31:42Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:35:54Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:41:04Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:46:19Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:51:35Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-29T23:56:52Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:02:18Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:07:33Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:12:50Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:18:00Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:23:04Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:28:06Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:33:33Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:38:48Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:43:56Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:49:00Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:54:26Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T00:59:29Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:04:34Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:09:37Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:14:48Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:19:54Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:25:12Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:30:29Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:35:34Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:40:49Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:45:59Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:51:12Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T01:56:14Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:01:28Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:06:40Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:11:42Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:16:54Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:22:13Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:27:28Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:32:30Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:37:46Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:42:58Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:48:10Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:53:35Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T02:58:48Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:04:02Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:09:23Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:14:34Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:19:35Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:24:45Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:29:50Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:35:00Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:40:02Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:45:20Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:50:24Z', 0, '2026-10-30T04:00:00Z', 0);
INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-30T03:55:37Z', 0, '2026-10-30T04:00:00Z', 0);

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
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T20:20:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T15:20:00Z', 'Under', 'ended at 92%', 92, 92, 9396151, 111, 0, NULL, NULL, 0, 0, 1, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T20:40:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T15:40:00Z', 'Under', 'ended at 0%', 0, 0, 27651, 57, 0, NULL, NULL, 0, 0, 0, 3, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T21:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T16:10:00Z', 'Under', 'ended at 57%', 57, 57, 8084309, 83, 0, NULL, NULL, 5, 9.4, 0, 0, 9, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-24T23:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T18:30:00Z', 'Under', 'ended at 89%', 89, 89, 12967902, 97, 0, NULL, NULL, 4, 5.3, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T01:20:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T20:20:00Z', 'Under', 'ended at 31%', 31, 31, 4173753, 57, 0, NULL, NULL, 0, 0, 1, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T02:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T21:10:00Z', 'Under', 'ended at 40%', 40, 40, 5498121, 93, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T04:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-24T23:30:00Z', 'Ok', 'ended at 95%', 95, 95, 13029023, 101, 0, NULL, NULL, 20, 32.9, 2, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T06:30:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T01:30:00Z', 'Under', 'ended at 71%', 71, 71, 8406012, 83, 0, NULL, NULL, 1, 3.3, 2, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T07:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T02:10:00Z', 'Blind', 'Claude''s meter unseen for 16m while 660.9k were spent', 97, 97, 12803190, 180, 0, NULL, NULL, 1, 0.1, 0, 0, 1, '[{"FromUtc":"2026-10-25T04:58:23Z","ToUtc":"2026-10-25T05:15:07Z","Fresh":660855,"Length":"00:16:44"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T09:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T04:30:00Z', 'Blind', 'Claude''s meter unseen for 15m while 394.4k were spent', 97, 97, 13413337, 130, 0, NULL, NULL, 12, 15, 0, 0, 1, '[{"FromUtc":"2026-10-25T04:59:20Z","ToUtc":"2026-10-25T05:15:19Z","Fresh":394424,"Length":"00:15:59"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T10:10:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T05:10:00Z', 'Under', 'ended at 0%', 0, 0, 0, 58, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T11:30:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T06:30:00Z', 'Under', 'ended at 47%', 47, 47, 4100425, 66, 0, NULL, NULL, 0, 0, 11, 11, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T12:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T07:10:00Z', 'Under', 'ended at 8%', 8, 8, 1019380, 53, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T14:50:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T09:50:00Z', 'Under', 'ended at 52%', 52, 52, 5934870, 89, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T19:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T14:20:00Z', 'Under', 'ended at 0%', 0, 0, 0, 60, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T20:10:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T15:10:00Z', 'Under', 'ended at 5%', 5, 5, 333456, 48, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-25T21:30:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T16:30:00Z', 'Under', 'ended at 33%', 33, 33, 5525012, 34, 0, NULL, NULL, 0, 0, 0, 0, 25, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T01:40:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T20:40:00Z', 'Blind', 'Claude''s meter unseen for 16m while 301.4k were spent', 56, 56, 7978587, 78, 0, NULL, NULL, 1, 0.5, 0, 0, 4, '[{"FromUtc":"2026-10-25T21:44:02Z","ToUtc":"2026-10-25T22:00:21Z","Fresh":301354,"Length":"00:16:19"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T02:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-25T21:30:00Z', 'Under', 'ended at 0%', 0, 0, 0, 58, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T05:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T00:20:00Z', 'Under', 'ended at 60%', 60, 60, 7942776, 83, 0, NULL, NULL, 2, 0.6, 1, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T06:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T01:40:00Z', 'Under', 'ended at 0%', 0, 0, 24672, 51, 0, NULL, NULL, 0, 0, 1, 2, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T07:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T02:00:00Z', 'Under', 'ended at 90%', 90, 90, 12529808, 102, 0, NULL, NULL, 5, 2.8, 3, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T07:10:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T02:10:00Z', 'Over', 'Claude reported 100%', 100, 100, 8562286, 104, 38.4, NULL, NULL, 5, 1.4, 8, 1, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T08:30:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T03:30:00Z', 'Under', 'ended at 0%', 0, 0, 0, 53, 0, NULL, NULL, 0, 0, 0, 1, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T10:20:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T05:20:00Z', 'Under', 'ended at 6%', 6, 6, 312352, 114, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T12:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T07:00:00Z', 'Ok', 'ended at 97%', 97, 97, 11023445, 107, 0, NULL, NULL, 1, 1.3, 1, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T12:10:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T07:10:00Z', 'Under', 'ended at 55%', 55, 55, 6212548, 86, 0, NULL, NULL, 0, 0, 3, 2, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T17:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T12:00:00Z', 'Under', 'ended at 50%', 50, 50, 5614821, 80, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T19:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T14:00:00Z', 'Under', 'ended at 94%', 94, 94, 11590204, 242, 0, NULL, NULL, 7, 6.2, 2, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T20:50:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T15:50:00Z', 'Under', 'ended at 0%', 0, 0, 0, 57, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T21:20:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T16:20:00Z', 'Under', 'ended at 0%', 0, 0, 0, 85, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T22:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T17:00:00Z', 'Under', 'ended at 57%', 57, 57, 6641345, 100, 0, NULL, NULL, 3, 6.2, 4, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-26T23:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T18:30:00Z', 'Under', 'ended at 52%', 52, 52, 4734844, 88, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T00:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T19:00:00Z', 'Under', 'ended at 68%', 68, 68, 10705244, 200, 0, NULL, NULL, 6, 9.3, 1, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T03:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T22:00:00Z', 'Under', 'ended at 11%', 11, 11, 865607, 90, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T04:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T23:10:00Z', 'Under', 'ended at 0%', 0, 0, 0, 58, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T04:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-26T23:30:00Z', 'Under', 'ended at 37%', 37, 37, 5302558, 78, 0, NULL, NULL, 2, 0.8, 5, 0, 2, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T05:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T00:00:00Z', 'Under', 'ended at 7%', 7, 7, 944348, 106, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T08:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T03:00:00Z', 'Over', 'Claude reported 100%', 100, 100, 10859144, 96, 187.5, NULL, NULL, 10, 25.5, 31, 7, 5, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T09:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T04:30:00Z', 'Under', 'ended at 23%', 23, 23, 1389678, 84, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T10:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T05:00:00Z', 'Under', 'ended at 86%', 86, 86, 15170850, 68, 0, NULL, NULL, 24, 30, 22, 0, 71, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T14:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T09:30:00Z', 'Under', 'ended at 27%', 27, 27, 3439047, 278, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T15:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T10:00:00Z', 'Under', 'ended at 15%', 15, 15, 2429503, 63, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T19:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T14:00:00Z', 'Under', 'ended at 14%', 14, 14, 1637552, 64, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T19:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T14:30:00Z', 'Under', 'ended at 0%', 0, 0, 0, 55, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T19:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T14:30:00Z', 'Under', 'ended at 29%', 29, 29, 3124352, 72, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T19:50:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T14:50:00Z', 'Under', 'ended at 0%', 0, 0, 0, 55, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-27T20:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T15:00:00Z', 'Under', 'ended at 71%', 71, 71, 12778523, 67, 0, NULL, NULL, 12, 21.2, 15, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T00:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T19:30:00Z', 'Under', 'ended at 48%', 48, 48, 8837494, 90, 0, NULL, NULL, 0, 0, 8, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T00:30:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T19:30:00Z', 'Under', 'ended at 37%', 37, 37, 6321547, 81, 0, NULL, NULL, 0, 0, 1, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T01:00:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T20:00:00Z', 'Under', 'ended at 20%', 20, 20, 3430571, 67, 0, NULL, NULL, 5, 5.8, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T02:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T21:40:00Z', 'Blind', 'Claude''s meter unseen for 15m while 24.2k were spent', 1, 1, 133106, 59, 0, NULL, NULL, 0, 0, 0, 0, 0, '[{"FromUtc":"2026-10-27T22:54:31Z","ToUtc":"2026-10-27T23:09:53Z","Fresh":24239,"Length":"00:15:22"}]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T05:30:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T00:30:00Z', 'Under', 'ended at 7%', 7, 7, 1383060, 68, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T06:00:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T01:00:00Z', 'Under', 'ended at 92%', 92, 92, 13178951, 88, 0, NULL, NULL, 11, 12.4, 10, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T06:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T01:10:00Z', 'Under', 'ended at 1%', 1, 1, 0, 59, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T06:40:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T01:40:00Z', 'Under', 'ended at 12%', 12, 12, 0, 68, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T11:00:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T06:00:00Z', 'Under', 'ended at 10%', 10, 10, 1332383, 183, 0, NULL, NULL, 1, 2.9, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T11:40:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T06:40:00Z', 'Under', 'ended at 7%', 7, 7, 0, 61, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T11:50:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T06:50:00Z', 'Under', 'ended at 1%', 1, 1, 0, 57, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T17:50:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T12:50:00Z', 'Under', 'ended at 81%', 81, 81, 12483873, 147, 0, NULL, NULL, 4, 8.3, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T19:00:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T14:00:00Z', 'Under', 'ended at 65%', 65, 65, 8005910, 89, 0, NULL, NULL, 0, 0, 3, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T19:10:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T14:10:00Z', 'Under', 'ended at 9%', 9, 9, 854474, 64, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T22:50:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T17:50:00Z', 'Under', 'ended at 25%', 25, 25, 2612853, 72, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-28T23:10:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T18:10:00Z', 'Under', 'ended at 2%', 2, 2, 299908, 59, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T04:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T23:10:00Z', 'Under', 'ended at 0%', 0, 0, 0, 56, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T04:40:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-28T23:40:00Z', 'Under', 'ended at 34%', 34, 34, 6834680, 80, 0, NULL, NULL, 4, 8.4, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T08:10:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T03:10:00Z', 'Under', 'ended at 0%', 0, 0, 0, 57, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T08:10:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T03:10:00Z', 'Under', 'ended at 4%', 4, 4, 665972, 62, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T09:40:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T04:40:00Z', 'Under', 'ended at 3%', 3, 3, 832515, 63, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T10:00:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T05:00:00Z', 'Under', 'ended at 0%', 0, 0, 0, 56, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T19:00:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T14:00:00Z', 'Under', 'ended at 28%', 28, 28, 2399636, 77, 0, NULL, NULL, 2, 0.8, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T19:30:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T14:30:00Z', 'Under', 'ended at 25%', 25, 25, 5641561, 76, 0, NULL, NULL, 3, 3.2, 11, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T19:40:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T14:40:00Z', 'Under', 'ended at 2%', 2, 2, 320835, 59, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-29T19:40:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T14:40:00Z', 'Under', 'ended at 13%', 13, 13, 1957331, 70, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T03:10:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T22:10:00Z', 'Under', 'ended at 8%', 8, 8, 973966, 63, 0, NULL, NULL, 0, 0, 0, 0, 1, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T03:20:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T22:20:00Z', 'Under', 'ended at 33%', 33, 33, 8744701, 61, 0, NULL, NULL, 42, 77.6, 36, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T04:00:00Z', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-29T23:00:00Z', 'Under', 'ended at 0%', 0, 0, 52967, 60, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T07:30:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T02:30:00Z', 'Under', 'ended at 34%', 34, 34, 6685354, 67, 0, NULL, NULL, 6, 14.3, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T08:20:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T03:20:00Z', 'Under', 'ended at 34%', 34, 34, 6468825, 72, 0, NULL, NULL, 5, 13.4, 14, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T08:40:00Z', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T03:40:00Z', 'Under', 'ended at 0%', 0, 0, 44477, 64, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T10:00:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T05:00:00Z', 'Under', 'ended at 9%', 9, 9, 1299260, 62, 0, NULL, NULL, 1, 0.2, 1, 1, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T19:00:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T14:00:00Z', 'Under', 'ended at 25%', 25, 25, 4598422, 85, 0, NULL, NULL, 1, 4.3, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-30T19:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T14:10:00Z', 'Under', 'ended at 2%', 2, 2, 367643, 59, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-31T00:10:00Z', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T19:10:00Z', 'Under', 'ended at 19%', 19, 19, 1641613, 69, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-31T00:10:00Z', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T19:10:00Z', 'Under', 'ended at 10%', 10, 10, 1130380, 81, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-31T00:20:00Z', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T19:20:00Z', 'Under', 'ended at 0%', 0, 0, 0, 55, 0, NULL, NULL, 0, 0, 0, 1, 0, '[]');
INSERT INTO pace_ledger (end_utc, account, start_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, held, held_minutes, refused, prompts_answered, exempt, gaps) VALUES ('2026-10-31T00:30:00Z', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-30T19:30:00Z', 'Under', 'ended at 3%', 3, 3, 349807, 60, 0, NULL, NULL, 0, 0, 0, 0, 0, '[]');

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
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-19', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 31670176);
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
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 31847988);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'GTFO.EZ', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4807881);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Launcher', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 186545);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Legion', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 284349);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-20', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 23924879);
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
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(outside workspace)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 245705);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(workspace root)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 12772972);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 152065);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1782588);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'ChiMesh', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 489103);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Claudia', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 64078);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Capture', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 90056);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Launcher', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 446040);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Orchestrator', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1467438);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2308727);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'OpenCredentials', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 32416);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'REA.Bloodlines', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16524668);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'REA.Bloodlines', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 54335);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'REA.Bloodlines', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7770642);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Cursory', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2689500);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Cursory', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 21886);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.GridGame', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2826980);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.Multiplayer', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 23184);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.SpaceGod', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7347955);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-22', 'Unity.SpaceGod', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9681293);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(outside workspace)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 70152);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(outside workspace)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1525098);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7692428);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 538389);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', 'billed:api-key', NULL, 690741);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3754319);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Agency', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 19649);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Launcher', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 94860);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Orchestrator', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3477982);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Orchestrator', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 20624973);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Orchestrator', 'Claude', 'billed:api-key', NULL, 98677);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'MindAttic.Tickets', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 19492);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Prose', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1534514);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'REA.Bloodlines', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1805270);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'REA.Bloodlines', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9482892);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'REA.Bloodlines', 'Claude', 'billed:api-key', NULL, 6599351);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Cursory', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 8498);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.Multiplayer', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1250923);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.SpaceGod', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9334947);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.SpaceGod', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17621786);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-23', 'Unity.SpaceGod', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 25808765);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(outside workspace)', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1199139);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(outside workspace)', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 38322);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(outside workspace)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 958080);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(workspace root)', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 23247938);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(workspace root)', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9089724);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(workspace root)', 'Claude', 'billed:api-key', NULL, 72900);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16102752);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Claudia', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 426091);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'GTFO.EZ.AbsurdMoves', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 68676);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'GTFO.EZ.AbsurdWeapons', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1418636);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MediaButler', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 41937);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Agency', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 898849);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Agency', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 90214);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Launcher', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 300402);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7263191);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 27651);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', 'billed:api-key', NULL, 4616053);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Orchestrator', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 574609);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'MindAttic.Tickets', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 93780);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'REA.Bloodlines', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7209489);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'REA.Bloodlines', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17986678);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-24', 'Unity.SpaceGod', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 10696394);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', '(outside workspace)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 456647);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', '(workspace root)', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2562818);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', '(workspace root)', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17106737);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 5470026);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 5606904);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Launcher', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 190755);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Orchestrator', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9890772);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Orchestrator', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 872781);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1437075);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Tickets', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 24672);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Tickets', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7105806);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'MindAttic.Tickets', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 141576);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'REA.Bloodlines', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4771406);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'REA.Bloodlines', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 5415758);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'RogueTrader.Brevity', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 275468);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'RusNet', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 520903);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'Unity.Cursory', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 6192);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'Unity.Eve', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4271);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'Unity.GridGame', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 6260);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-25', 'Unity.SpaceGod', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1577013);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', '(outside workspace)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17144);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', '(outside workspace)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 288163);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', '(workspace root)', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 13265871);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 39755013);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'GTFO.CrashTestDummies', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1037776);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'MindAttic.Orchestrator', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4105811);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 199884);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'MindAttic.Tickets', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 99657);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 214895);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'REA.Bloodlines', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 15952063);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'RogueTrader.Brevity', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 9799518);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-26', 'RogueTrader.Brevity', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1874639);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', '(workspace root)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 133106);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', '(workspace root)', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3339392);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1604699);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T22:00:00Z', 1637552);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'MindAttic.Capture', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 494252);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'MindAttic.Launcher', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 160281);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'MindAttic.Orchestrator', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2917701);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'MindAttic.Orchestrator', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T22:00:00Z', 12552774);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 167186);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'MindAttic.Tickets', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T22:00:00Z', 1002401);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'Prose', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 4042603);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2363572);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'Prose', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T22:00:00Z', 30834601);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'REA.Bloodlines', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1224311);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'REA.Bloodlines', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T22:00:00Z', 1070995);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'REA.Bloodlines', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 228531);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'REA.Bloodlines', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', '2026-10-27T22:00:00Z', 5267074);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'RogueTrader.Brevity', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 130150);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-27', 'RogueTrader.Overwatch', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 335083);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 299908);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', '(workspace root)', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 12936723);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'AutoGamePlay', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 24868);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'MindAttic.Orchestrator', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7780443);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'MindAttic.Orchestrator', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 854474);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'Prose', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 6202157);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'REA.Bloodlines', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 8005910);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'REA.Bloodlines', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 747333);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'RogueTrader.Overwatch', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 396390);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-28', 'Unity.GridGame', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 16473);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', '(workspace root)', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 351142);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3180663);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', '(workspace root)', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 780409);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', '(workspace root)', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 157220);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', 'MindAttic.Orchestrator', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 5884768);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', 'MindAttic.Orchestrator', 'Claude', 'billed:api-key', NULL, 415835);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', 'Prose', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 225812);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', 'Prose', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 17945487);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', 'RogueTrader.Brevity', 'Claude', '1a3ae0a8-1412-43d4-a4b9-278387ae4d00/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 22660);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-29', 'RogueTrader.Overwatch', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 3530441);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', '(workspace root)', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 2422492);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'IdiotProof', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 5841044);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'MindAttic.Orchestrator', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1621098);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'MindAttic.Orchestrator', 'Claude', 'billed:api-key', NULL, 1072241);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'MindAttic.Vault', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 358996);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'Prose', 'Claude', '2544fa20-81fb-425b-9d08-f32c585a9205/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 7563272);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'RogueTrader.Brevity', 'Claude', '278b4195-962e-489a-bc6f-29c407304223/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 81296);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'RogueTrader.Overwatch', 'Claude', '8675f09e-2ea3-4c0b-9af4-1ea1c5001fbf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 349807);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'RogueTrader.Overwatch', 'Claude', 'd769f8c5-9906-44e8-adee-fbc651f355bf/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 495398);
INSERT INTO usage_history (day, repo, provider, account, before_utc, fresh) VALUES ('2026-10-30', 'Unity.Rea.Hare', 'Claude', '1a45d147-abaf-4098-9f29-54375717dfd7/96142256-0cfd-4191-90f1-9c7987cb184a', NULL, 1299260);

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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('02889e88-a4db-44a6-a372-1006a24f19e2/agent-acb3986c89fc6e9b0', 'PRS-T133', 'chore', 'Prose', 1024064, '2026-10-30T04:01:00.671Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('02890e56-5257-49dd-8659-245102519bfa', 'GE-T3', 'chore', 'GTFO.EZ', 1749292, '2026-10-15T09:45:02.94Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('02cca644-9a1f-4424-813d-edfe444baa65', 'SS-T88', 'chore', 'Prose', 373434, '2026-10-15T20:48:59.345Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('030e5a5e-44f3-4b5a-a229-4d9707cf5903', 'GG-T21', 'chore', 'GridGameGaiden', 52863, '2026-10-15T11:13:00.014Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('034a58c5-8a10-4f41-9766-506a3cce90e4', 'USS-T39', 'feature', 'UnitySolarSystem', 290571, '2026-10-15T11:18:48.065Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('03d2bb3a-0964-49b7-b836-0a69a6049c38', 'M.ORCH-T901', 'fix', 'MindAttic.Orchestrator', 6296, '2026-10-26T04:39:21.994Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('03ddf508-a736-4d57-a26c-3350b33e18b1', 'SS-T38', 'chore', 'Prose', 245228, '2026-10-15T12:54:52.624Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('04815c10-9d98-447d-a364-6fdbdc337e70', 'M.ORCH-T704', 'fix', 'MindAttic.Orchestrator', 20193, '2026-10-23T19:54:59.584Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('053545c4-2407-4c70-b0d0-4f1a0533e4e8', 'GG-T18', 'feature', 'GridGameGaiden', 27421, '2026-10-15T13:12:27.381Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('05637a82-0d68-46c4-b77c-798a6a0c30f6', 'AUT-T11', 'chore', 'Automata', 62425, '2026-10-16T00:56:27.827Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0657de29-2ef7-498f-b99b-937d9deef28b', 'MAO-T84', 'feature', 'MindAttic.Orchestrator', 749664, '2026-10-15T08:18:31.745Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0687c7fd-a751-4729-a87c-00812f3d32e9', 'M.CAPT-T2', 'chore', 'MindAttic.Capture', 551651, '2026-10-16T02:05:24.47Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a0194dd8e70e2dfcd', 'UQD-T53', 'chore', '(workspace root)', 89107, '2026-10-26T05:10:48.512Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a019f28ccd972b96e', 'U.RICO-T28', 'fix', '(workspace root)', 240994, '2026-10-26T06:29:24.911Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a0478c3c95d483894', 'U.RICO-T43', 'feature', '(workspace root)', 79436, '2026-10-26T08:29:50.169Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a053aa97638065d82', 'U.RICO-T76', 'fix', '(workspace root)', 86003, '2026-10-26T06:44:08.469Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a06c96d1564729f87', 'U.OVW-T20', 'feature', '(workspace root)', 466141, '2026-10-26T11:48:20.631Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a070331c2b8b344e8', 'U.HARE-T86', 'fix', '(workspace root)', 78241, '2026-10-26T13:03:05.471Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a09db314cda09a0f3', 'U.HARE-T27', 'feature', '(workspace root)', 1283595, '2026-10-26T12:16:18.53Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a0f4ea42b1c673dbc', 'U.RICO-T27', 'fix', '(workspace root)', 156022, '2026-10-26T14:16:56.529Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a120ed25600149b1d', 'U.RICO-T40', 'chore', '(workspace root)', 94049, '2026-10-26T06:09:04.422Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a13c959516448fe97', 'U.RICO-T29', 'fix', '(workspace root)', 172284, '2026-10-26T09:16:23.598Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a155620f0b2baa5af', 'U.CHN-T75', 'chore', '(workspace root)', 64488, '2026-10-26T05:23:30.92Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a19120369577130e2', 'U.HARE-T59', 'feature', '(workspace root)', 207377, '2026-10-26T10:56:32.404Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a1acfcea1d032276f', 'U.HARE-T25', 'feature', '(workspace root)', 302865, '2026-10-26T10:39:57.125Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a1cff8791d41e0d34', 'U.OVW-T7', 'chore', '(workspace root)', 81221, '2026-10-26T04:10:38.085Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a1d2fcbd580185971', 'U.HARE-T81', 'fix', '(workspace root)', 87281, '2026-10-26T08:27:07.321Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a21e4f37c9a945b2e', 'U.RICO-T42', 'feature', '(workspace root)', 324394, '2026-10-26T07:02:17.187Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a226d2e63ae420c5b', 'U.OVW-T8', 'feature', '(workspace root)', 280872, '2026-10-26T04:32:15.768Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a23c78fcb036b6825', 'U.RICO-T26', 'fix', '(workspace root)', 80422, '2026-10-26T07:41:37.339Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a24186c536a569b64', 'U.HARE-T23', 'feature', '(workspace root)', 439272, '2026-10-26T07:04:03.358Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a29c7ecdae65c67a9', 'UQD-T50', 'feature', '(workspace root)', 169919, '2026-10-26T04:21:19.314Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a2bcedd89cdfeb7a4', 'UQD-T54', 'chore', '(workspace root)', 124202, '2026-10-26T06:07:34.944Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a2d99c24fa79db69c', 'U.RICO-T44', 'feature', '(workspace root)', 115959, '2026-10-26T09:20:37.25Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a30d716c6c8a7818f', 'U.OVW-T14', 'feature', '(workspace root)', 276800, '2026-10-26T07:00:08.406Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a34095dbe473e7192', 'U.INC-T35', 'chore', '(workspace root)', 331825, '2026-10-26T04:45:44.319Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a3f6d5348fb4fc565', 'U.HARE-T60', 'feature', '(workspace root)', 370401, '2026-10-26T12:36:33.88Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a459c2ac14ca99505', 'U.HARE-T53', 'feature', '(workspace root)', 815383, '2026-10-26T05:39:01.966Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a46383b548dcc960f', 'U.OVW-T15', 'feature', '(workspace root)', 241945, '2026-10-26T07:07:04.152Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4703d32700c53b65', 'U.RICO-T75', 'fix', '(workspace root)', 118055, '2026-10-26T06:56:09.583Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a475f44a0520da870', 'U.HARE-T20', 'feature', '(workspace root)', 238860, '2026-10-26T07:51:05.498Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4855b0efcb6be1fd', 'U.RICO-T20', 'fix', '(workspace root)', 81684, '2026-10-26T04:18:58.906Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4a2848ff7c07b3d7', 'U.RICO-T25', 'fix', '(workspace root)', 163883, '2026-10-26T06:38:09.434Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4a5ff5822aca8546', 'U.HARE-T62', 'feature', '(workspace root)', 477994, '2026-10-26T14:21:35.021Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4ab99ea1b95bfb16', 'U.OVW-T16', 'feature', '(workspace root)', 720235, '2026-10-26T08:08:34.524Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4b741027ecb71dfa', 'U.RICO-T73', 'fix', '(workspace root)', 110158, '2026-10-26T06:21:46.898Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4c829fde95fb6084', 'U.RICO-T46', 'feature', '(workspace root)', 349674, '2026-10-26T11:31:31.153Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4d1a82240adf38f9', 'U.OVW-T22', 'feature', '(workspace root)', 462252, '2026-10-26T13:52:53.687Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a4daa5f4d72c829ff', 'U.OVW-T13', 'feature', '(workspace root)', 293262, '2026-10-26T06:48:41.994Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a55626f54be5dd984', 'UQD-T52', 'chore', '(workspace root)', 40102, '2026-10-26T04:51:26.247Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a5748beb9fc0f7e54', 'UQD-T62', 'fix', '(workspace root)', 85336, '2026-10-26T04:38:14.486Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a5a7213f85ffb6cd3', 'U.OVW-T18', 'feature', '(workspace root)', 524051, '2026-10-26T10:14:00.383Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a5b7139d8081956ab', 'U.RICO-T45', 'feature', '(workspace root)', 197646, '2026-10-26T11:07:09.935Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a60efa21a1cc73af0', 'U.RICO-T77', 'fix', '(workspace root)', 144582, '2026-10-26T08:12:50.712Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a64b17de5831c4238', 'U.HARE-T85', 'fix', '(workspace root)', 40367, '2026-10-26T12:23:25.346Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a6b9a54e6b25214f4', 'U.OVW-T12', 'feature', '(workspace root)', 344601, '2026-10-26T06:31:34.512Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a6d0c04b34fd80084', 'U.HARE-T84', 'fix', '(workspace root)', 76476, '2026-10-26T12:18:53.739Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a6ed8aba4b33c792f', 'U.RICO-T47', 'feature', '(workspace root)', 742311, '2026-10-26T12:06:05.068Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a72e10030a39aeeca', 'U.RICO-T50', 'feature', '(workspace root)', 445201, '2026-10-26T13:57:59.078Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a75beafdb40dad572', 'U.OVW-T43', 'fix', '(workspace root)', 76744, '2026-10-26T12:01:19.777Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a7f49938c3611d2ef', 'U.RICO-T39', 'chore', '(workspace root)', 220835, '2026-10-26T05:48:59.183Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a816b4be064df0567', 'U.OVW-T10', 'feature', '(workspace root)', 278308, '2026-10-26T05:31:36.375Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a86ebc1c7f365d73b', 'U.CHN-T63', 'chore', '(workspace root)', 148729, '2026-10-26T04:44:10.799Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a8f2346052dd5d050', 'U.HARE-T22', 'feature', '(workspace root)', 345074, '2026-10-26T10:08:45.927Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a995e76de9b933375', 'U.CHN-T45', 'chore', '(workspace root)', 39153, '2026-10-26T04:02:05.26Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a999aae23d77558b1', 'UQD-T54', 'chore', '(workspace root)', 51192, '2026-10-26T05:18:38.047Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a9a371aebeaa7ecfd', 'U.RICO-T44', 'feature', '(workspace root)', 921063, '2026-10-26T10:39:33.875Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a9b1f0018c10e3ff6', 'U.OVW-T9', 'feature', '(workspace root)', 255351, '2026-10-26T04:43:29.717Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a9c21e97294665fd8', 'U.RICO-T38', 'chore', '(workspace root)', 278791, '2026-10-26T05:14:28.336Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a9d7ea84c0215f0e1', 'U.RICO-T49', 'feature', '(workspace root)', 394002, '2026-10-26T13:19:15.629Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a9de95030d11197b8', 'U.RICO-T72', 'fix', '(workspace root)', 233414, '2026-10-26T15:44:47.959Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-a9f902b1c8166b0ed', 'U.OVW-T19', 'feature', '(workspace root)', 373443, '2026-10-26T10:46:51.169Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aa01aef4d71bed147', 'U.RICO-T43', 'feature', '(workspace root)', 147617, '2026-10-26T09:01:51.209Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aa5206274b3a78b6d', 'U.HARE-T51', 'feature', '(workspace root)', 120466, '2026-10-26T04:04:57.344Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aa59c14e654e3e017', 'U.HARE-T61', 'feature', '(workspace root)', 265131, '2026-10-26T13:07:46.929Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aa9dac9cbefc09115', 'U.HARE-T82', 'fix', '(workspace root)', 45780, '2026-10-26T09:12:02.1Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aaab64e27857d965e', 'U.RICO-T36', 'chore', '(workspace root)', 45644, '2026-10-26T04:08:04.749Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aab5b91a7929f901d', 'U.HARE-T52', 'feature', '(workspace root)', 1074928, '2026-10-26T04:52:03.563Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aad54339457a97e8c', 'U.HARE-T26', 'feature', '(workspace root)', 423801, '2026-10-26T13:47:21.595Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aaef00b2e35db97a0', 'U.HARE-T22', 'feature', '(workspace root)', 231594, '2026-10-26T09:27:17.097Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aaf008ff59b619c8d', 'U.HARE-T54', 'feature', '(workspace root)', 310381, '2026-10-26T06:17:09.716Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aaf21c58d9a0400f5', 'U.HARE-T18', 'feature', '(workspace root)', 228617, '2026-10-26T07:39:03.212Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ab39bfb74a9a8e985', 'U.CHN-T62', 'chore', '(workspace root)', 221120, '2026-10-26T04:33:11.448Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ab6c88b15325c08f7', 'U.HARE-T58', 'feature', '(workspace root)', 328082, '2026-10-26T09:18:03.158Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ab73c7788ec759fb9', 'U.RICO-T74', 'fix', '(workspace root)', 29182, '2026-10-26T06:13:54.365Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-abe80160b9d5958f5', 'U.RICO-T41', 'feature', '(workspace root)', 140324, '2026-10-26T06:15:30.739Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ac3f8f9ad95117362', 'U.OVW-T21', 'feature', '(workspace root)', 362362, '2026-10-26T13:15:33.931Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ac7c487a4c008704c', 'U.HARE-T83', 'fix', '(workspace root)', 71233, '2026-10-26T11:07:57.245Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ac9dbc7dad6a8ed73', 'U.HARE-T57', 'feature', '(workspace root)', 399484, '2026-10-26T08:18:06.346Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-acef51dadfbdd77d5', 'U.OVW-T18', 'feature', '(workspace root)', 233110, '2026-10-26T09:33:05.144Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ad040b15f7f5860e8', 'U.HARE-T55', 'feature', '(workspace root)', 361384, '2026-10-26T06:33:53.619Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ad617852189f41989', 'U.OVW-T11', 'feature', '(workspace root)', 241812, '2026-10-26T06:14:24.98Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ad846990456eeb936', 'U.RICO-T48', 'feature', '(workspace root)', 530591, '2026-10-26T12:43:26.087Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-adac9a665697d996a', 'U.HARE-T21', 'feature', '(workspace root)', 75461, '2026-10-26T08:19:49.811Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-add3f795cdfb229d0', 'U.RICO-T43', 'feature', '(workspace root)', 281877, '2026-10-26T08:14:36.761Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ae54dc1b4248d29fc', 'U.HARE-T21', 'feature', '(workspace root)', 257915, '2026-10-26T09:03:01.621Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-ae974a17d0577b5c7', 'U.RICO-T32', 'fix', '(workspace root)', 107795, '2026-10-26T11:25:13.575Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aedde4e53e320f91f', 'U.HARE-T31', 'feature', '(workspace root)', 250437, '2026-10-26T06:44:20.593Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-aef4b18374c2da1cb', 'U.OVW-T15', 'feature', '(workspace root)', 176582, '2026-10-26T07:27:48.622Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-af107445e92979a49', 'U.OVW-T17', 'feature', '(workspace root)', 568361, '2026-10-26T09:17:36.634Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-af168f6a2f3bfc587', 'U.INC-T39', 'feature', '(workspace root)', 519622, '2026-10-26T05:25:10.184Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-af1d9acd3dc4cf109', 'U.RICO-T37', 'chore', '(workspace root)', 92180, '2026-10-26T04:35:50.258Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-af1dac84e32bc67d4', 'UQD-T51', 'chore', '(workspace root)', 157691, '2026-10-26T04:41:28.255Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-af4dd97f276462b33', 'U.HARE-T56', 'feature', '(workspace root)', 236810, '2026-10-26T07:19:04.022Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('069b8f3a-083e-419e-a4c0-48bc816cfb6f/agent-afc0b5eaa0a00afbc', 'U.OVW-T9', 'feature', '(workspace root)', 109733, '2026-10-26T05:15:39.399Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('073cd0ba-725b-4465-9f0f-71fd9c93a2aa', 'M.ORCH-T653', 'fix', 'MindAttic.Orchestrator', 8056, '2026-10-23T01:40:59.199Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('07744b63-e590-491f-bd99-e4aac4027ca1', 'U.HARE-T41', 'chore', 'Unity.Hare', 64108, '2026-10-24T19:30:12.483Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('07b1a8a4-58ec-449a-8a80-13ca30b52068', 'U.RICO-T27', 'fix', 'Unity.Ricochet', 31402, '2026-10-26T14:14:58.945Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('090436c7-c731-48fd-8e1d-23070d467dca', 'U.RON-T2', 'chore', 'UnityRonin', 78248, '2026-10-16T01:30:25.95Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('098daffe-3b0a-4f04-a52e-bc8c0d2ef0ad', 'M.WEB-T10', 'docs', 'MindAttic.Web', 15879, '2026-10-21T18:10:42.43Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0a037c4e-86cc-47e8-ac9d-be8e76bdf4a5', 'RB-T187', 'fix', 'REA.Bloodlines', 27071, '2026-10-25T02:56:50.448Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-a3cfe9c237caed6b0', 'G.EZ-T26', 'chore', 'GTFO.EZ', 404945, '2026-10-18T03:03:31.362Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-a8a88167f7ced5ec9', 'G.EZ-T28', 'chore', 'GTFO.EZ', 188662, '2026-10-18T02:52:41.9Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-ae14e91077f74649e', 'G.EZ-T27', 'chore', 'GTFO.EZ', 31506, '2026-10-18T02:49:02.782Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ab1f970-9b99-4fe7-aba8-ce2f76bdc744/agent-af704ff01b2a3d501', 'G.EZ-T29', 'docs', 'GTFO.EZ', 107781, '2026-10-18T02:51:43.736Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0ce1d76e-5332-4e7c-b641-5307d28b8a5d', 'M.ORCH-T625', 'fix', 'MindAttic.Orchestrator', 19763, '2026-10-22T17:42:49.864Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0cf09747-4c38-4ca3-89a5-0fc92989bebf', 'PRS-T217', 'docs', 'Prose', 52010, '2026-10-27T07:34:32.072Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0e3d9912-af74-4f05-8c25-2603c82585c1', 'M.ORCH-T732', 'fix', 'MindAttic.Orchestrator', 7443, '2026-10-23T20:25:52.192Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0e7342a0-81df-4f84-b4f2-fbda7f46a44b', 'M.ORCH-T600', 'fix', 'MindAttic.Orchestrator', 27086, '2026-10-22T00:33:57.917Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0f0deb42-1b30-43f2-ad51-ee05e2037c8a', 'SS-T95', 'chore', 'Prose', 129365, '2026-10-15T18:18:26.471Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0f5d1472-92bc-4241-9d95-fa6f19cd2d87', 'SS-T57', 'chore', 'Prose', 81168, '2026-10-15T12:44:26.548Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0f8387ba-fa84-4f28-b8cb-da38cf0a879d', 'M.ORCH-T740', 'fix', 'MindAttic.Orchestrator', 53732, '2026-10-23T20:26:06.771Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fb16d99-8258-40cf-aa00-3f107247bc6f', 'U.OVW-T7', 'chore', 'Unity.Overworld', 52756, '2026-10-26T04:08:51.927Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fd3fa21-5da2-43af-b334-1fef4f486b65', 'UQD-T29', 'chore', 'Unity.QuickDuel', 49234, '2026-10-24T11:40:54.613Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fd44d81-7b7d-434c-b3ad-95a54c42f8c5/agent-a116d5d2e90e82881', 'U.RICO-T36', 'chore', 'Unity.Ricochet', 328141, '2026-10-24T19:15:48.356Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fd44d81-7b7d-434c-b3ad-95a54c42f8c5/agent-a6920354a3be680f0', 'U.RICO-T36', 'chore', 'Unity.Ricochet', 378298, '2026-10-24T20:15:36.845Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fd44d81-7b7d-434c-b3ad-95a54c42f8c5/agent-a769f93d83d996454', 'U.RICO-T36', 'chore', 'Unity.Ricochet', 13977, '2026-10-24T21:30:14.357Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('0fd44d81-7b7d-434c-b3ad-95a54c42f8c5/agent-a92cd81ebe56d122d', 'U.RICO-T36', 'chore', 'Unity.Ricochet', 142682, '2026-10-24T22:14:20.848Z', 0, 'sonnet');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('11a99b6a-ae71-4a44-a14e-85a3b94b993c', 'U.RICO-T40', 'chore', 'Unity.Ricochet', 17754, '2026-10-26T06:05:39.182Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a066bd8ce5625036e', 'RB-T156', 'feature', 'REA.Bloodlines', 731103, '2026-10-25T04:26:41.493Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a0837ddef75534772', 'RB-T201', 'fix', 'REA.Bloodlines', 254843, '2026-10-25T04:45:04.328Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a0e1ef8594ff2eafa', 'RB-T167', 'feature', 'REA.Bloodlines', 222342, '2026-10-25T06:48:21.614Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a0e61d72fdd48f7df', 'RB-T186', 'feature', 'REA.Bloodlines', 265099, '2026-10-25T06:46:27.189Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a12c9248b6dba3e7a', 'RB-T194', 'fix', 'REA.Bloodlines', 325765, '2026-10-25T02:57:49.096Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a216ab28b2f9dbf7b', 'RB-T140', 'feature', 'REA.Bloodlines', 625367, '2026-10-25T02:13:47.843Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a2f6af43bbf43d219', 'RB-T199', 'fix', 'REA.Bloodlines', 250177, '2026-10-25T03:58:55.312Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a366c27c67bcf205f', 'RB-T153', 'feature', 'REA.Bloodlines', 545740, '2026-10-25T02:37:13.064Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a398dfb4b17ef9703', 'RB-T141', 'feature', 'REA.Bloodlines', 328979, '2026-10-25T01:56:15.775Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a445b70d45a75847e', 'RB-T188', 'feature', 'REA.Bloodlines', 592521, '2026-10-25T07:20:03.736Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a491a3ef2f6dfd874', 'RB-T164', 'feature', 'REA.Bloodlines', 138208, '2026-10-25T06:51:36.054Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a58aa41384858d690', 'RB-T172', 'chore', 'REA.Bloodlines', 310886, '2026-10-25T07:10:40.766Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a69824994e8788d50', 'RB-T187', 'fix', 'REA.Bloodlines', 1130650, '2026-10-25T03:13:07.084Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a6fbea5ac607492ee', 'RB-T202', 'fix', 'REA.Bloodlines', 359535, '2026-10-25T05:09:40.445Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a720a40f2f0d985a3', 'RB-T197', 'fix', 'REA.Bloodlines', 81272, '2026-10-25T02:45:58.455Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a7caa9e8c12a71f55', 'RB-T191', 'feature', 'REA.Bloodlines', 352847, '2026-10-25T07:10:12.139Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a90689aec2b094875', 'RB-T199', 'fix', 'REA.Bloodlines', 160218, '2026-10-25T03:15:08.612Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a914141477e531ce9', 'RB-T189', 'feature', 'REA.Bloodlines', 412287, '2026-10-25T07:10:04.494Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a91ac5a56f06967f4', 'RB-T110', 'feature', 'REA.Bloodlines', 218420, '2026-10-25T01:49:33.698Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a91f7269b729df390', 'RB-T142', 'feature', 'REA.Bloodlines', 425132, '2026-10-25T02:04:10.536Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a933cc17fb5a86210', 'RB-T187', 'fix', 'REA.Bloodlines', 628186, '2026-10-25T04:58:49.568Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a95d844031214f11d', 'RB-T180', 'feature', 'REA.Bloodlines', 237419, '2026-10-25T06:51:28.184Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-a9d60edaaa8efe372', 'RB-T190', 'feature', 'REA.Bloodlines', 608064, '2026-10-25T07:15:04.219Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-aa0d38bef969966b2', 'RB-T156', 'feature', 'REA.Bloodlines', 310315, '2026-10-25T03:03:11.903Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-aa1382f6a3b6f8f98', 'RB-T157', 'feature', 'REA.Bloodlines', 239620, '2026-10-25T03:21:11.098Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-aa6c937392cc13e73', 'RB-T176', 'feature', 'REA.Bloodlines', 438917, '2026-10-25T07:10:15.107Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-aab98504196ab57ac', 'RB-T177', 'feature', 'REA.Bloodlines', 153897, '2026-10-25T06:44:13.827Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-ab06b01c21e4b6c41', 'RB-T199', 'fix', 'REA.Bloodlines', 55024, '2026-10-25T04:32:15.053Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-ac0b1794c0b42d1d7', 'RB-T170', 'feature', 'REA.Bloodlines', 286429, '2026-10-25T07:02:32.609Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-ad07508f8700f5176', 'RB-T179', 'feature', 'REA.Bloodlines', 540012, '2026-10-25T07:10:02.941Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-ae573df86c441f436', 'RB-T192', 'feature', 'REA.Bloodlines', 364421, '2026-10-25T07:10:55.609Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-ae6531ebcc2c3bb8a', 'RB-T168', 'feature', 'REA.Bloodlines', 152314, '2026-10-25T06:41:10.366Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-aeecafd99a194ad8c', 'RB-T151', 'feature', 'REA.Bloodlines', 308527, '2026-10-25T01:55:32.005Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-af6c1250535178d27', 'RB-T185', 'feature', 'REA.Bloodlines', 119532, '2026-10-25T06:39:52.073Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12795dbb-cadb-4053-a46e-bb2241b3f3bc/agent-af8eaa92ff4bb61ca', 'RB-T200', 'fix', 'REA.Bloodlines', 234881, '2026-10-25T04:40:09.023Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a084e94ff7e4ea775', 'U.HARE-T48', 'feature', 'Unity.Hare', 219959, '2026-10-25T02:16:23.527Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a218a9b2b8a024b05', 'U.HARE-T46', 'feature', 'Unity.Hare', 291037, '2026-10-25T01:36:24.574Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a22a3d6ea43573f54', 'U.HARE-T48', 'feature', 'Unity.Hare', 353651, '2026-10-25T03:18:53.935Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a24621e1ceafbf8b6', 'U.HARE-T44', 'chore', 'Unity.Hare', 486751, '2026-10-24T21:21:29.443Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a24876c7689f3bf4d', 'U.HARE-T47', 'feature', 'Unity.Hare', 308774, '2026-10-25T02:00:47.353Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a368dd417fbc53309', 'U.HARE-T35', 'feature', 'Unity.Hare', 408685, '2026-10-25T04:28:23.213Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a4fb8168d17a6244b', 'U.HARE-T49', 'feature', 'Unity.Hare', 335446, '2026-10-25T03:51:03.666Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a63fe19a80c860ad8', 'U.HARE-T50', 'feature', 'Unity.Hare', 307427, '2026-10-25T04:38:12.463Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a714258624084c700', 'U.HARE-T45', 'chore', 'Unity.Hare', 147862, '2026-10-24T21:26:03.539Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-a9353598de3e9110e', 'U.HARE-T43', 'chore', 'Unity.Hare', 237538, '2026-10-24T20:40:10.786Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-aa4bab0efe2e9baed', 'U.HARE-T41', 'chore', 'Unity.Hare', 642582, '2026-10-24T19:39:40.988Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-ab4ae644bfe9a4fa4', 'U.HARE-T45', 'chore', 'Unity.Hare', 774817, '2026-10-24T22:44:44.538Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12bdc36c-d887-4849-870e-5e969c25fa82/agent-aee9bb1157472556c', 'U.HARE-T42', 'chore', 'Unity.Hare', 855882, '2026-10-24T20:44:14.343Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a05a5c6274fb22fa1', 'U.RICO-T117', 'feature', '(workspace root)', 118806, '2026-10-30T16:37:09.455Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a06eb187cb6abd855', 'U.OVW-T51', 'feature', '(workspace root)', 138569, '2026-10-30T16:36:50.1Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a0ef75fbf934a6f17', 'U.RICO-T146', 'chore', '(workspace root)', 104759, '2026-10-30T16:37:20.036Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a15341bd731a52d72', 'UQD-T78', 'chore', '(workspace root)', 118878, '2026-10-30T16:38:36.717Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a2e062621eeec833e', 'U.HARE-T104', 'chore', '(workspace root)', 85425, '2026-10-30T16:35:37.616Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a3cef39fd936d9db6', 'U.HARE-T113', 'fix', '(workspace root)', 107187, '2026-10-30T15:54:02.75Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a44739649d654b4d4', 'U.VAN-T33', 'chore', '(workspace root)', 103754, '2026-10-30T16:39:28.092Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a4efefd788fb89dc3', 'U.HARE-T107', 'chore', '(workspace root)', 113791, '2026-10-30T16:38:29.752Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a56968bcedccf0e89', 'U.RICO-T116', 'feature', '(workspace root)', 118532, '2026-10-30T16:36:50.384Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a7987aae5c5c36736', 'U.HARE-T103', 'docs', '(workspace root)', 120216, '2026-10-30T16:39:16.386Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a9905e8e8f737f10f', 'U.CHN-T100', 'chore', '(workspace root)', 153708, '2026-10-30T16:36:40.044Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-a9f508e5caaa942de', 'U.RICO-T118', 'feature', '(workspace root)', 123580, '2026-10-30T16:36:39.331Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-aa3e836b86d858f85', 'U.RICO-T149', 'chore', '(workspace root)', 158034, '2026-10-30T16:37:20.824Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-aa4f08cfa96a971d5', 'U.RICO-T151', 'fix', '(workspace root)', 85504, '2026-10-30T15:50:49.982Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-abb2ec21241716e4a', 'U.INC-T45', 'chore', '(workspace root)', 107057, '2026-10-30T16:37:18.867Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-ad836be7b4bfd1157', 'U.RICO-T147', 'docs', '(workspace root)', 137455, '2026-10-30T16:36:41.722Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('12fd3110-ee17-448d-84de-55fa5e99f3f9/agent-adf76e34c872a7f52', 'U.HARE-T105', 'chore', '(workspace root)', 112468, '2026-10-30T16:38:06.113Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13726e3f-c886-41e5-8853-42793579bd32/agent-a51b5bdd7325957ae', 'U.VAN-T18', 'feature', 'Unity.ProjectVania', 260663, '2026-10-25T01:35:42.551Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13726e3f-c886-41e5-8853-42793579bd32/agent-a81b1f22abba19993', 'U.VAN-T25', 'feature', 'Unity.ProjectVania', 366934, '2026-10-25T01:47:30.348Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13726e3f-c886-41e5-8853-42793579bd32/agent-a825c5212c28628de', 'U.VAN-T19', 'chore', 'Unity.ProjectVania', 363893, '2026-10-25T02:09:13.346Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13726e3f-c886-41e5-8853-42793579bd32/agent-a943305cd778250f8', 'U.VAN-T21', 'chore', 'Unity.ProjectVania', 507882, '2026-10-25T03:37:42.074Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13726e3f-c886-41e5-8853-42793579bd32/agent-ae35b7465e3f858dd', 'U.VAN-T20', 'chore', 'Unity.ProjectVania', 307315, '2026-10-25T02:47:45.034Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('13cb820a-3dc3-4951-a193-e21d77771fe8', 'SS-T14', 'chore', 'Prose', 32733, '2026-10-15T11:58:04.131Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1404abba-0362-4878-8b72-c568ac5ce656', 'CUR-T15', 'docs', 'Cursory', 19526, '2026-10-21T18:11:13.839Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('140574bc-26c8-4d4d-92e1-c0012e600219', 'CUR-T2', 'chore', 'Cursory', 45531, '2026-10-16T00:54:58.889Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('140e61ae-c9f0-4f26-be4d-b2bbed71cb9b', 'U.SPC-T586', 'fix', 'Unity.SpaceGod', 30608, '2026-10-25T05:31:09.81Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('148b8b16-e5d1-4ab7-8d91-38127b3579f4', 'M.TIX-T91', 'chore', 'MindAttic.Tickets', 34550, '2026-10-25T23:00:34.576Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('15cf5108-874e-4e5e-91a5-366c125f93cb', 'PRS-T16', 'fix', 'Prose', 425165, '2026-10-16T07:27:38.953Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('15f72e02-ad83-4020-96f8-d3ce6aba82c7', 'OC-T20', 'docs', 'OpenCredentials', 18014, '2026-10-21T18:11:43.275Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('160bb34c-06aa-4d20-ae84-967580cf3179', 'SS-T17', 'chore', 'Prose', 317446, '2026-10-15T12:49:09.975Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1666ddb2-941f-457a-8556-1ee04abb499e', 'U.SPC-T535', 'fix', 'Unity.SpaceGod', 165861, '2026-10-24T23:02:17.132Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('178cf8c5-938c-4e2c-83c0-01f3e3bb628b', 'U.INC-T25', 'chore', 'Unity.Incognito', 23744, '2026-10-24T13:25:52.415Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('17c76877-4c52-4335-b7d0-0e516a026985', 'GG-T50', 'feature', 'GridGameGaiden', 261741, '2026-10-15T16:19:18.202Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1836d17d-b4aa-44ce-9a05-806e4c289e7e', 'PRS-T15', 'fix', 'Prose', 214629, '2026-10-16T00:56:27.24Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('18646bb1-e071-4e3c-8f1d-d803c1ee7d8b', 'SS-T68', 'chore', 'Prose', 230431, '2026-10-15T18:39:44.203Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('19160327-a771-46b3-ae83-bbd663e6ad75', 'U.RICO-T29', 'fix', 'Unity.Ricochet', 24106, '2026-10-26T09:14:17.204Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('193fa3a0-18d0-473a-b49e-683b2af22292', 'GG-T42', 'feature', 'GridGameGaiden', 225349, '2026-10-15T14:26:40.89Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('197e90de-626c-4306-a6c3-9d44e960f129', 'U.OVW-T6', 'chore', 'Unity.Overworld', 123170, '2026-10-24T10:31:13.999Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a000f81883acc9d52', 'U.SPC-T537', 'fix', 'Unity.SpaceGod', 164125, '2026-10-25T04:02:59.994Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a048d8d59ddcd40c5', 'U.SPC-T478', 'feature', 'Unity.SpaceGod', 195679, '2026-10-24T19:27:21.552Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a0528cc03d39c3b5a', 'U.SPC-T535', 'fix', 'Unity.SpaceGod', 67417, '2026-10-24T23:03:25.794Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a061cb0f462ab7ed9', 'U.SPC-T486', 'feature', 'Unity.SpaceGod', 45673, '2026-10-24T19:16:11.441Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a072f5318ed7a7533', 'U.SPC-T572', 'fix', 'Unity.SpaceGod', 48713, '2026-10-25T05:28:39.957Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a08b572061169f6be', 'U.SPC-T566', 'fix', 'Unity.SpaceGod', 118280, '2026-10-25T05:31:39.738Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a0f2e80c5a8ecf158', 'U.SPC-T533', 'feature', 'Unity.SpaceGod', 311734, '2026-10-24T22:57:59.028Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a1231079960ad7287', 'U.SPC-T534', 'feature', 'Unity.SpaceGod', 52424, '2026-10-25T00:34:19.448Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a17d847bd4651e6b5', 'U.SPC-T581', 'fix', 'Unity.SpaceGod', 48021, '2026-10-25T05:29:45.213Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a1b5c21f9532e84a1', 'U.SPC-T505', 'feature', 'Unity.SpaceGod', 208304, '2026-10-24T19:53:21.517Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a1d342e1ed942d728', 'U.SPC-T560', 'fix', 'Unity.SpaceGod', 32711, '2026-10-25T04:56:04.487Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a2a68977510c8ded0', 'U.SPC-T545', 'fix', 'Unity.SpaceGod', 35174, '2026-10-25T04:04:06.611Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a2b597f19711660c8', 'U.SPC-T567', 'fix', 'Unity.SpaceGod', 28685, '2026-10-25T05:03:44.564Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a2f5926ebe6bb7dee', 'U.SPC-T584', 'fix', 'Unity.SpaceGod', 31973, '2026-10-25T05:27:57.33Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a2f6cd21d27d62314', 'U.SPC-T557', 'fix', 'Unity.SpaceGod', 72023, '2026-10-25T05:20:42.283Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a33a2c0767e3f4551', 'U.SPC-T547', 'fix', 'Unity.SpaceGod', 69725, '2026-10-25T04:27:35.301Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a3984895ff14b1b1a', 'U.SPC-T478', 'feature', 'Unity.SpaceGod', 138502, '2026-10-24T22:20:00.578Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a3b184bb74df8ed45', 'U.SPC-T568', 'fix', 'Unity.SpaceGod', 28983, '2026-10-25T05:22:10.539Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a4283b3d8af66c99e', 'U.SPC-T587', 'fix', 'Unity.SpaceGod', 108183, '2026-10-25T05:51:07.334Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a467a4d51700beb14', 'U.SPC-T574', 'fix', 'Unity.SpaceGod', 43163, '2026-10-25T05:24:36.653Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a4fb976fcc4d1d88f', 'U.SPC-T577', 'fix', 'Unity.SpaceGod', 22402, '2026-10-25T05:28:51.849Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a54b4b0e23740710c', 'U.SPC-T557', 'fix', 'Unity.SpaceGod', 96662, '2026-10-25T04:43:35.964Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a54bf05c7c8486200', 'U.SPC-T588', 'fix', 'Unity.SpaceGod', 61603, '2026-10-25T05:53:58.502Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a5721f79c711fb201', 'U.SPC-T546', 'fix', 'Unity.SpaceGod', 29735, '2026-10-25T04:09:49.227Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a57cb08bfe88d59a8', 'U.SPC-T478', 'feature', 'Unity.SpaceGod', 40502, '2026-10-24T21:30:31.679Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a5a957cce4bdf8c39', 'U.SPC-T552', 'fix', 'Unity.SpaceGod', 35629, '2026-10-25T04:33:20.79Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a5e6870010e5e915c', 'U.SPC-T569', 'fix', 'Unity.SpaceGod', 42088, '2026-10-25T05:29:12.937Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a5ff08e4f4f3851b1', 'U.SPC-T555', 'fix', 'Unity.SpaceGod', 28163, '2026-10-25T04:35:01.921Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a6230ad7118ba6bd7', 'U.SPC-T575', 'fix', 'Unity.SpaceGod', 20744, '2026-10-25T05:17:49.186Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a6c9f252d1725f10f', 'U.SPC-T586', 'fix', 'Unity.SpaceGod', 45229, '2026-10-25T05:33:05.676Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a6d1806ace91281da', 'U.SPC-T541', 'fix', 'Unity.SpaceGod', 18785, '2026-10-25T04:23:25.034Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a6ddc9be686e5fa01', 'U.SPC-T478', 'feature', 'Unity.SpaceGod', 204817, '2026-10-24T20:03:08.887Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a6fc8d3b5f2ded755', 'U.SPC-T570', 'fix', 'Unity.SpaceGod', 30297, '2026-10-25T05:08:17.72Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a73163c60a8c4fdd3', 'U.SPC-T502', 'feature', 'Unity.SpaceGod', 836112, '2026-10-24T21:18:58.663Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a734eb38559ed8125', 'U.SPC-T564', 'fix', 'Unity.SpaceGod', 38780, '2026-10-25T05:02:39.029Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a74134a259d30bebc', 'U.SPC-T531', 'fix', 'Unity.SpaceGod', 84352, '2026-10-24T19:17:19.583Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a741dfa8e21efe595', 'U.SPC-T582', 'fix', 'Unity.SpaceGod', 53617, '2026-10-25T05:29:57.597Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a75e5a9d59ed3b944', 'U.SPC-T580', 'fix', 'Unity.SpaceGod', 36616, '2026-10-25T05:24:26.207Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a7900e155f2ccb302', 'U.SPC-T571', 'fix', 'Unity.SpaceGod', 48161, '2026-10-25T05:07:53Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a7ecfdc75ad14ea10', 'U.SPC-T530', 'fix', 'Unity.SpaceGod', 17399, '2026-10-24T19:10:55.008Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a86aa2f3bcfe45974', 'U.SPC-T555', 'fix', 'Unity.SpaceGod', 18939, '2026-10-25T04:33:58.921Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a8eeab3f2b2940dec', 'U.SPC-T562', 'fix', 'Unity.SpaceGod', 38070, '2026-10-25T04:55:18.972Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a9126820e4d2ec026', 'U.SPC-T502', 'feature', 'Unity.SpaceGod', 135688, '2026-10-24T21:59:57.064Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a92031669a5ff14ee', 'U.SPC-T583', 'fix', 'Unity.SpaceGod', 35114, '2026-10-25T05:33:42.78Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a96132a00b53f0d03', 'U.SPC-T543', 'fix', 'Unity.SpaceGod', 34554, '2026-10-25T04:00:59.452Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a96b5601118172b1d', 'U.SPC-T554', 'fix', 'Unity.SpaceGod', 28307, '2026-10-25T04:32:58.117Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a980799f611b5d2d5', 'U.SPC-T540', 'fix', 'Unity.SpaceGod', 23289, '2026-10-25T03:55:49.806Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-a9a7fc70d2c1baf1f', 'U.SPC-T550', 'fix', 'Unity.SpaceGod', 36729, '2026-10-25T04:33:15.423Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aa14eeca1cf6950fa', 'U.SPC-T534', 'feature', 'Unity.SpaceGod', 1169641, '2026-10-24T23:58:20.429Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aa40538421a08048f', 'U.SPC-T553', 'fix', 'Unity.SpaceGod', 28120, '2026-10-25T04:29:56.754Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aa7ffd4e9a51f80a7', 'U.SPC-T491', 'feature', 'Unity.SpaceGod', 41538, '2026-10-24T19:22:24.43Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aaae4e2bc60b4e1f2', 'U.SPC-T563', 'fix', 'Unity.SpaceGod', 33516, '2026-10-25T04:56:26.678Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aac83f4352966c5ba', 'U.SPC-T573', 'fix', 'Unity.SpaceGod', 58374, '2026-10-25T05:28:25.182Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aad8f9d9796357be3', 'U.SPC-T533', 'feature', 'Unity.SpaceGod', 641015, '2026-10-24T22:12:10.551Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aaf1dcded209532fe', 'U.SPC-T507', 'feature', 'Unity.SpaceGod', 271826, '2026-10-24T20:03:04.349Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-abdaf33859ab6b5d2', 'U.SPC-T561', 'fix', 'Unity.SpaceGod', 128821, '2026-10-25T05:20:49.252Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ac0a962c22eb9cdf1', 'U.SPC-T541', 'fix', 'Unity.SpaceGod', 44689, '2026-10-25T04:11:36.499Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ac23aa4725936c9cb', 'U.SPC-T544', 'fix', 'Unity.SpaceGod', 29142, '2026-10-25T03:59:14.477Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ac9495d0e3dd7b492', 'U.SPC-T559', 'fix', 'Unity.SpaceGod', 48810, '2026-10-25T04:52:07.215Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-acdf33318c1f066e0', 'U.SPC-T565', 'fix', 'Unity.SpaceGod', 27517, '2026-10-25T04:59:50.773Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ad3473efcbed319fb', 'U.SPC-T558', 'fix', 'Unity.SpaceGod', 27405, '2026-10-25T04:47:32.979Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ad378071d3690467e', 'U.SPC-T585', 'fix', 'Unity.SpaceGod', 155509, '2026-10-25T05:41:37.029Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ad47a02b93b99ff48', 'U.SPC-T556', 'fix', 'Unity.SpaceGod', 20479, '2026-10-25T04:35:52.111Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ad57bb31c5fed270e', 'U.SPC-T498', 'feature', 'Unity.SpaceGod', 90545, '2026-10-24T19:27:32.323Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ad63a9a23c4dd2cb1', 'U.SPC-T533', 'feature', 'Unity.SpaceGod', 38445, '2026-10-24T21:40:37.886Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-adbd18783ac18e00e', 'U.SPC-T501', 'feature', 'Unity.SpaceGod', 198165, '2026-10-24T19:39:17.879Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-add6b94bdb1173e48', 'U.SPC-T578', 'fix', 'Unity.SpaceGod', 31988, '2026-10-25T05:29:56.297Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ae03282b4fc23f38c', 'U.SPC-T548', 'fix', 'Unity.SpaceGod', 40257, '2026-10-25T04:33:20.366Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ae41da0b9f97c3049', 'U.SPC-T538', 'fix', 'Unity.SpaceGod', 40102, '2026-10-25T03:39:50.349Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ae7b4410dd517810b', 'U.SPC-T529', 'fix', 'Unity.SpaceGod', 45921, '2026-10-24T19:11:16.727Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-ae8be61576b7c6c2f', 'U.SPC-T576', 'fix', 'Unity.SpaceGod', 26276, '2026-10-25T05:22:36.505Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-aed9acb84d288b5c1', 'U.SPC-T549', 'fix', 'Unity.SpaceGod', 34944, '2026-10-25T04:33:13.556Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-af08ecd153c292e14', 'U.SPC-T478', 'feature', 'Unity.SpaceGod', 375319, '2026-10-24T20:49:20.312Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-af383bae1e17dabc1', 'U.SPC-T539', 'fix', 'Unity.SpaceGod', 65860, '2026-10-25T03:49:15.981Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-af52d299be252394e', 'U.SPC-T542', 'fix', 'Unity.SpaceGod', 42009, '2026-10-25T04:05:18.448Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-af64cd9443e6fcf82', 'U.SPC-T501', 'feature', 'Unity.SpaceGod', 436244, '2026-10-24T20:32:05.789Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-af9a2cd8877298fc8', 'U.SPC-T579', 'fix', 'Unity.SpaceGod', 45152, '2026-10-25T05:30:36.793Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('1987ec86-9f1f-42b9-b2e9-9f56bbbce08d/agent-afad347e6987dd985', 'U.SPC-T551', 'fix', 'Unity.SpaceGod', 34379, '2026-10-25T04:33:02.182Z', 0, 'sonnet');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('22f4d780-4910-45a5-9459-4f7c14f4bec7', 'M.ORCH-T982', 'fix', 'MindAttic.Orchestrator', 62727, '2026-10-26T18:41:48.717Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2305b05e-e329-4d82-92a1-75d3fd03d38b', 'U.SPC-T397', 'chore', 'Unity.SpaceGod', 23097, '2026-10-22T10:08:49.274Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('23c96b9d-6918-4855-81df-44992e462d4c', 'SS-T25', 'chore', 'Prose', 19465, '2026-10-15T11:56:34.07Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('24c789dc-69cb-49bf-974c-1ec48da0b50d', 'GG-T37', 'feature', 'GridGameGaiden', 89563, '2026-10-15T13:37:25.28Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('25e72d46-fc7c-4e32-937d-19b9d0f97fb0', 'UNI-T22', 'fix', 'Unity.Chain', 46848, '2026-10-23T22:15:31.51Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('27fe7ce7-120f-4946-9130-122a3879b678', 'U.SPC-T41', 'feature', 'UnitySolarSystem', 159769, '2026-10-15T11:19:43.96Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a0305f9fddcf895ca', 'U.RICO-T139', 'fix', 'Unity.Ricochet', 36098, '2026-10-28T14:48:31.164Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a083274fdf2405973', 'U.RICO-T87', 'chore', 'Unity.Ricochet', 37100, '2026-10-28T15:49:16.549Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a0b272aa68af98ebd', 'U.CHN-T95', 'fix', 'Unity.Ricochet', 49899, '2026-10-28T14:28:23.544Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a1082c97656cac3c8', 'U.HARE-T30', 'feature', 'Unity.Ricochet', 160174, '2026-10-28T15:46:17.429Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a112dc123645f3782', 'U.RICO-T145', 'fix', 'Unity.Ricochet', 33180, '2026-10-28T15:40:16.321Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a1a283e1fcbe19e5c', 'U.CHN-T88', 'fix', 'Unity.Ricochet', 179818, '2026-10-28T15:34:38.959Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a20eda20b615d5d5a', 'U.RICO-T122', 'fix', '(workspace root)', 24743, '2026-10-28T04:00:59.878Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a2756768a94dd159e', 'U.RICO-T134', 'fix', 'Unity.Ricochet', 178330, '2026-10-28T15:02:10.005Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a2a6a7e03258763db', 'U.RICO-T53', 'feature', '(workspace root)', 146158, '2026-10-28T04:08:08.879Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a2cd9c79a5c534ea6', 'U.RICO-T143', 'fix', 'Unity.Ricochet', 21007, '2026-10-28T15:19:01.175Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a2de84c31380ed97e', 'U.RICO-T106', 'fix', '(workspace root)', 54956, '2026-10-28T03:15:46.451Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a2f79bf309b30d298', 'GG-T179', 'feature', '(workspace root)', 244048, '2026-10-28T03:00:17.058Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a34bcd9178f80dcb2', 'U.HARE-T64', 'feature', '(workspace root)', 123263, '2026-10-28T03:51:18.291Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a373d3822de1dcb7d', 'U.OVW-T47', 'feature', 'Unity.Ricochet', 186580, '2026-10-28T15:52:45.877Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a373d3e7cebf28703', 'U.HARE-T67', 'feature', '(workspace root)', 241811, '2026-10-28T02:59:30.675Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a376c2ebd5d3540f2', 'U.RICO-T129', 'fix', 'Unity.Ricochet', 25976, '2026-10-28T14:12:37.868Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a3d613dd2a3739743', 'U.RICO-T52', 'feature', '(workspace root)', 228477, '2026-10-28T02:59:36.229Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a442f5df11ea349bd', 'U.RICO-T133', 'fix', 'Unity.Ricochet', 25353, '2026-10-28T14:20:20.254Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a46f1985c8adeb62b', 'U.HARE-T99', 'fix', 'Unity.Ricochet', 56026, '2026-10-28T15:15:06.412Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a481041c9f417e79c', 'U.RICO-T137', 'fix', 'Unity.Ricochet', 24235, '2026-10-28T14:44:54.231Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a483ce4ac7490c665', 'U.RICO-T92', 'fix', 'Unity.Ricochet', 51396, '2026-10-28T15:26:46.586Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a5227e0bac83adf08', 'U.RICO-T125', 'fix', '(workspace root)', 28094, '2026-10-28T04:04:08.674Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a5888931259595e8e', 'U.RICO-T103', 'fix', '(workspace root)', 57862, '2026-10-28T13:54:10.145Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a5893355f38f74450', 'U.RICO-T106', 'fix', '(workspace root)', 120012, '2026-10-28T04:03:13.107Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a589f23212d0dcb95', 'U.RICO-T104', 'fix', '(workspace root)', 95317, '2026-10-28T03:51:20.572Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a5bd84611bd609fc6', 'U.RICO-T105', 'fix', '(workspace root)', 30829, '2026-10-28T03:14:58.052Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a5ee4e2662467a56d', 'U.RICO-T54', 'feature', '(workspace root)', 30296, '2026-10-28T13:09:12.474Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a61d3a03665077b9b', 'U.HARE-T95', 'fix', 'Unity.Ricochet', 218835, '2026-10-28T14:39:47.665Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a620b18dd2eec7263', 'U.RICO-T96', 'fix', 'Unity.Ricochet', 131716, '2026-10-28T14:26:15.678Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a641a7ef09858766d', 'U.RICO-T55', 'feature', 'Unity.Ricochet', 144504, '2026-10-28T14:57:04.568Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a6430e89517311c9e', 'U.OVW-T46', 'feature', 'Unity.Ricochet', 196221, '2026-10-28T15:52:42.608Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a65b83ade2e324b6c', 'U.RICO-T101', 'fix', '(workspace root)', 85654, '2026-10-28T14:09:50.248Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a68507c2522cb2af7', 'U.HARE-T69', 'docs', 'Unity.Ricochet', 102667, '2026-10-28T15:49:16.539Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a6bed18ac521a6d28', 'U.HARE-T100', 'fix', 'Unity.Ricochet', 66513, '2026-10-28T15:24:50.454Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a6e807d6af31e467d', 'U.HARE-T65', 'feature', '(workspace root)', 169648, '2026-10-28T13:58:04.009Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a7194bb352ac214f7', 'U.HARE-T65', 'feature', 'Unity.Ricochet', 52435, '2026-10-28T14:42:40.064Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a7523d32859f0bf89', 'U.RICO-T144', 'fix', 'Unity.Ricochet', 29487, '2026-10-28T15:39:11.886Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a7a6ae669eada45aa', 'U.HARE-T90', 'fix', 'Unity.Ricochet', 105886, '2026-10-28T15:38:21.459Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a7acfb936a8f16816', 'UQD-T71', 'docs', 'Unity.Ricochet', 59505, '2026-10-28T15:48:55.45Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a83cb2b7a6dfdc7dd', 'U.RICO-T91', 'fix', 'Unity.Ricochet', 51562, '2026-10-28T15:27:37.927Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a84737ad59c13cf43', 'U.CHN-T87', 'fix', 'Unity.Ricochet', 53870, '2026-10-28T15:29:27.689Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a86b15ec1a3c01976', 'U.RICO-T120', 'fix', '(workspace root)', 22209, '2026-10-28T03:16:13.255Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a87c18d39c227f888', 'U.HARE-T68', 'feature', '(workspace root)', 286417, '2026-10-28T14:07:11.208Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a8ad39debc0ca325f', 'U.RICO-T94', 'fix', 'Unity.Ricochet', 83369, '2026-10-28T15:16:45.482Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a8ade10c50999334f', 'U.HARE-T94', 'docs', 'Unity.Ricochet', 127337, '2026-10-28T14:34:39.694Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a8faa46b014fe2c77', 'U.RICO-T126', 'fix', '(workspace root)', 70847, '2026-10-28T12:57:24.699Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a90cd72aeea187acb', 'U.RICO-T54', 'feature', 'Unity.Ricochet', 218782, '2026-10-28T14:51:32.176Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a950ced81a9682795', 'U.RICO-T109', 'fix', '(workspace root)', 80864, '2026-10-28T03:15:09.628Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a9b28a411a6c4a7f3', 'U.RICO-T110', 'fix', '(workspace root)', 78686, '2026-10-28T02:58:54.105Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a9ef581bcc0a85a2a', 'U.RICO-T127', 'fix', 'Unity.Ricochet', 31229, '2026-10-28T14:11:22.489Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-a9fba2fa796b9c2dd', 'GG-T180', 'feature', '(workspace root)', 139033, '2026-10-28T13:00:06.236Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aa190b80614f14cfd', 'U.HARE-T24', 'feature', '(workspace root)', 152633, '2026-10-28T04:08:10.629Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aa1bfd0009da4a6e9', 'U.RICO-T93', 'fix', 'Unity.Ricochet', 78993, '2026-10-28T15:14:53.347Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aa7b32092cb029f0e', 'U.RICO-T56', 'feature', 'Unity.Ricochet', 173603, '2026-10-28T15:10:05.68Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aa818f03d9caf601b', 'U.HARE-T24', 'feature', '(workspace root)', 257704, '2026-10-28T13:05:24.755Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aaa806b2b5c35e0b9', 'U.RICO-T135', 'fix', 'Unity.Ricochet', 43251, '2026-10-28T14:28:55.306Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aaed57aee703d1f27', 'U.RICO-T143', 'fix', 'Unity.Ricochet', 47684, '2026-10-28T15:56:34.393Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aaff03d86537c6d99', 'U.RICO-T104', 'fix', '(workspace root)', 99529, '2026-10-28T03:19:23.858Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ab0c7633a697941d5', 'U.RICO-T141', 'fix', 'Unity.Ricochet', 173716, '2026-10-28T15:18:27.739Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ab323b3aa996590f2', 'U.RICO-T57', 'feature', 'Unity.Ricochet', 198778, '2026-10-28T15:17:27.098Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ab591de2b42ebd055', 'U.HARE-T68', 'feature', '(workspace root)', 190732, '2026-10-28T04:07:58.571Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ab82d31ec43e117d0', 'U.RICO-T130', 'fix', 'Unity.Ricochet', 53742, '2026-10-28T14:19:25.796Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ab94fcf5ad30615e4', 'U.RICO-T97', 'fix', 'Unity.Ricochet', 44780, '2026-10-28T14:10:24.677Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ab9c101b12bdb1139', 'U.RICO-T140', 'fix', 'Unity.Ricochet', 84965, '2026-10-28T15:14:43.221Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-abcc8e65770a4aec6', 'U.RICO-T142', 'fix', 'Unity.Ricochet', 22970, '2026-10-28T15:20:50.241Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-abe57de2190d25444', 'U.RICO-T121', 'fix', '(workspace root)', 28313, '2026-10-28T03:20:16.691Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-abec48908f14967fb', 'U.RICO-T100', 'fix', '(workspace root)', 34652, '2026-10-28T14:08:54.59Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-abfa5060106667603', 'U.HARE-T68', 'feature', '(workspace root)', 331350, '2026-10-28T13:07:31.719Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ac32b790cb446fc3a', 'U.RICO-T124', 'fix', '(workspace root)', 29903, '2026-10-28T04:01:59.988Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ac745c4032789eb48', 'U.HARE-T67', 'feature', '(workspace root)', 113286, '2026-10-28T03:51:36.904Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-acbcacd18d59f50a0', 'U.HARE-T65', 'feature', '(workspace root)', 59502, '2026-10-28T13:08:31.829Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-accd4fe0f79a0cc3b', 'U.HARE-T71', 'chore', 'Unity.SpaceGod', 44557, '2026-10-28T18:13:16.778Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ad5085acb141b4aee', 'U.RICO-T52', 'feature', '(workspace root)', 209427, '2026-10-28T03:55:52.945Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ada5a7577b2502ddc', 'U.RICO-T138', 'fix', 'Unity.Ricochet', 37366, '2026-10-28T14:46:38.38Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-adc0fd25e1f6b1ccb', 'U.EVE-T41', 'feature', 'Unity.Ricochet', 134017, '2026-10-28T15:55:44.614Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-adc3711e3fac5fb9c', 'U.HARE-T91', 'fix', 'Unity.Ricochet', 71771, '2026-10-28T15:28:01.923Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ae2a325b0f5650a58', 'U.HARE-T68', 'feature', 'Unity.Ricochet', 88464, '2026-10-28T14:52:19.204Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ae4416055bbd54eda', 'U.HARE-T29', 'feature', 'Unity.Ricochet', 82273, '2026-10-28T15:31:09.617Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ae5432b2dc268d194', 'U.HARE-T101', 'fix', 'Unity.Ricochet', 35001, '2026-10-28T15:22:24.379Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ae62e1d63cc0bc119', 'U.HARE-T64', 'feature', '(workspace root)', 313720, '2026-10-28T02:59:31.645Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ae6a511b4ed64272b', 'U.RICO-T102', 'fix', '(workspace root)', 97856, '2026-10-28T14:09:03.66Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-ae765d56898449369', 'U.RICO-T53', 'feature', '(workspace root)', 86723, '2026-10-28T12:58:18.234Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-aec66aadd081cb4a4', 'GG-T179', 'feature', '(workspace root)', 306096, '2026-10-28T03:59:14.636Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-af15f20ef1434924e', 'U.RICO-T54', 'feature', '(workspace root)', 166119, '2026-10-28T13:57:57.681Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-af2ff72e41779dfbe', 'U.RICO-T57', 'feature', 'Unity.Ricochet', 122203, '2026-10-28T15:52:41.556Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-af48905e01ab2a8f4', 'U.HARE-T102', 'fix', 'Unity.Ricochet', 37959, '2026-10-28T15:47:45.631Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-af6937293dbe9696f', 'U.HARE-T98', 'fix', 'Unity.Ricochet', 90538, '2026-10-28T15:14:08.969Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-af6bc964c7086f988', 'U.HARE-T29', 'feature', 'Unity.Ricochet', 158436, '2026-10-28T14:58:35.445Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-af6cc61008efdc125', 'U.RICO-T95', 'fix', 'Unity.Ricochet', 108395, '2026-10-28T14:30:38.297Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-afa2eee6b430915fd', 'U.HARE-T95', 'fix', 'Unity.Ricochet', 27749, '2026-10-28T14:53:27.063Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2825e89a-daff-4fa9-bdbd-36e47a65193e/agent-afe89c87fc4411c25', 'U.RICO-T143', 'fix', 'Unity.Ricochet', 21085, '2026-10-28T15:19:32.584Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('290a401f-e0f6-4678-aac3-361ea19a5492', 'GG-T45', 'feature', 'GridGameGaiden', 326932, '2026-10-15T15:32:45.457Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2966ca2e-c808-4bb3-b839-b10cd5d088b5', 'U.CHN-T71', 'chore', 'Unity.Chain', 42415, '2026-10-24T19:40:26.975Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291', 'A-T1', '', 'MindAttic.Orchestrator', 3226400, '2026-10-27T22:57:49.942Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a0a43b7925819bd3b', 'M.ORCH-T1181', 'fix', 'MindAttic.Orchestrator', 113591, '2026-10-27T22:54:37.305Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a0bf6b6d7b4aa4346', 'M.ORCH-T1069', 'fix', 'MindAttic.Orchestrator', 133419, '2026-10-27T21:17:26.449Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a0e0a3ec1a94e042d', 'M.ORCH-T1069', 'fix', 'MindAttic.Orchestrator', 36197, '2026-10-27T22:48:37.546Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a101cc21f5d198f23', 'M.ORCH-T1184', 'fix', 'MindAttic.Orchestrator', 114362, '2026-10-27T22:54:51.418Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a14c2937f80c253ab', 'M.ORCH-T1185', 'fix', 'MindAttic.Orchestrator', 48156, '2026-10-27T22:51:07.689Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a1eaa3d9ba78e04aa', 'M.ORCH-T1188', 'fix', 'MindAttic.Orchestrator', 105816, '2026-10-27T22:54:41.159Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a20c2f87075d0346f', 'M.ORCH-T1187', 'fix', 'MindAttic.Orchestrator', 48015, '2026-10-27T22:50:31.314Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a23489f7ab0f3b3f9', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 212623, '2026-10-27T22:55:42.981Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a23ea70fda8a49f7a', 'M.ORCH-T1178', 'fix', 'MindAttic.Orchestrator', 88275, '2026-10-27T22:53:42.201Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a296888512748570d', 'M.ORCH-T1186', 'fix', 'MindAttic.Orchestrator', 119128, '2026-10-27T22:57:34.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a2a698710d19a26d5', 'M.ORCH-T1169', 'fix', 'MindAttic.Orchestrator', 106626, '2026-10-27T21:45:43.773Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a2d34c71afcdf4c17', 'M.ORCH-T813', 'fix', 'MindAttic.Orchestrator', 105897, '2026-10-27T21:16:35.889Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a4062ac243628124e', 'M.ORCH-T1093', 'fix', 'MindAttic.Orchestrator', 107633, '2026-10-27T21:16:16.943Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a47a594a5b2162b5e', 'M.ORCH-T1174', 'fix', 'MindAttic.Orchestrator', 53542, '2026-10-27T22:47:46.995Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a50fc9372bbaadb5f', 'M.ORCH-T1182', 'fix', 'MindAttic.Orchestrator', 130850, '2026-10-27T22:54:42.509Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a58ece2f765fffb0d', 'M.ORCH-T1190', 'fix', 'MindAttic.Orchestrator', 73948, '2026-10-27T22:52:22.794Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a5a434f34380cf01f', 'M.ORCH-T1175', 'fix', 'MindAttic.Orchestrator', 71683, '2026-10-27T21:44:16.361Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a70bac9eaa518dbda', 'M.ORCH-T1151', 'fix', 'MindAttic.Orchestrator', 54882, '2026-10-27T20:07:08.154Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a76517c363c45d4fa', 'M.ORCH-T1183', 'fix', 'MindAttic.Orchestrator', 125226, '2026-10-27T22:55:15.761Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a7e4f58b9fee309da', 'M.ORCH-T1051', 'fix', 'MindAttic.Orchestrator', 46050, '2026-10-27T20:03:58.437Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a7e8c7144cabf99d3', 'M.ORCH-T758', 'feature', 'MindAttic.Orchestrator', 139294, '2026-10-27T21:16:09.088Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a85ad2efa2155a8fe', 'M.ORCH-T814', 'feature', 'MindAttic.Orchestrator', 38092, '2026-10-27T21:11:45.831Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a86b30f91c23f93b8', 'M.ORCH-T1152', 'fix', 'MindAttic.Orchestrator', 63210, '2026-10-27T21:11:57.051Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a894d5b45d2a0355d', 'M.ORCH-T1114', 'fix', 'MindAttic.Orchestrator', 155686, '2026-10-27T21:16:48.496Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-a95af05d91780d98d', 'M.ORCH-T1189', 'fix', 'MindAttic.Orchestrator', 69002, '2026-10-27T22:51:39.914Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-ab8e8cd044d8e9482', 'M.ORCH-T815', 'feature', 'MindAttic.Orchestrator', 27206, '2026-10-27T21:11:14.307Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-acad0451a83c3dd47', 'M.ORCH-T1177', 'fix', 'MindAttic.Orchestrator', 142183, '2026-10-27T22:54:30.884Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-adec163dccc64f945', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 154278, '2026-10-27T21:17:29.277Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-ae3319dbe9817ba34', 'M.ORCH-T1105', 'fix', 'MindAttic.Orchestrator', 47286, '2026-10-27T20:05:57.387Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2dd2b796-793e-4c61-836e-de83457d1291/agent-aef9b10620f4828f2', 'M.ORCH-T1174', 'fix', 'MindAttic.Orchestrator', 93522, '2026-10-27T21:48:17.644Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2e308308-cd8b-4cca-8742-4651ad3c572e', 'M.ORCH-T843', 'chore', 'MindAttic.Orchestrator', 6901, '2026-10-26T02:59:09.765Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2e908411-5591-4107-9c97-c17511fa302b', 'MAT-T53', 'docs', 'MindAttic.Agency', 46134, '2026-10-24T06:44:29.524Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2efe68bb-8df4-4797-a605-fc49205bbc52', 'GG-T39', 'feature', 'GridGameGaiden', 437324, '2026-10-15T14:22:03.227Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2f1aced6-85ec-4a7f-ac09-7168b62b2aa5', 'U.SPC-T33', 'chore', 'UnitySolarSystem', 184785, '2026-10-15T10:55:30.855Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2f7dc099-518a-40fb-a305-1c69ff7107c3', 'U.VAN-T4', 'chore', 'Unity.ProjectVania', 13596, '2026-10-23T22:54:21.824Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('2f869b18-505e-4dd4-baa4-dd182fc97114', 'AUT-T7', 'chore', 'Automata', 124342, '2026-10-16T00:28:11.312Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('30a9bdc9-e00e-42c1-9728-ab2496765886', 'SS-T84', 'chore', 'Prose', 884833, '2026-10-15T20:34:37.505Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('320acbe9-0043-4941-be4d-2741433f8e7d', 'GG-T41', 'feature', 'GridGameGaiden', 326932, '2026-10-15T15:05:18.842Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a023186c3865e1453', 'RB-T118', 'feature', 'REA.Bloodlines', 186923, '2026-10-24T19:28:36.335Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a08b1b3f175c16db5', 'RB-T136', 'feature', 'REA.Bloodlines', 507534, '2026-10-24T21:29:31.486Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a165a8a6c5e9adbd6', 'RB-T142', 'feature', 'REA.Bloodlines', 319159, '2026-10-24T22:17:07.543Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a1e4fa31c3001603b', 'RB-T152', 'chore', 'REA.Bloodlines', 140030, '2026-10-24T19:57:24.722Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a20534b0283984b4d', 'RB-T128', 'feature', 'REA.Bloodlines', 173096, '2026-10-24T20:03:37.023Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a21f037d31b7957e0', 'RB-T138', 'feature', 'REA.Bloodlines', 14348, '2026-10-24T21:33:49.6Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a2d894f3447cccd18', 'RB-T184', 'fix', 'REA.Bloodlines', 53503, '2026-10-24T22:26:31.584Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a369b3f6680f3ca0f', 'RB-T182', 'fix', 'REA.Bloodlines', 320796, '2026-10-24T19:32:00.855Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a5200cdfe70340ec2', 'RB-T141', 'feature', 'REA.Bloodlines', 528280, '2026-10-24T22:14:35.126Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a63550e8e5df1ac82', 'RB-T140', 'feature', 'REA.Bloodlines', 22056, '2026-10-24T21:34:33.769Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a637c427a20800a77', 'RB-T121', 'feature', 'REA.Bloodlines', 218662, '2026-10-24T20:03:03.068Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a663b33f9be60cbc3', 'RB-T181', 'fix', 'REA.Bloodlines', 377231, '2026-10-24T19:30:08.367Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a67438740663078d3', 'RB-T152', 'chore', 'REA.Bloodlines', 118320, '2026-10-24T19:17:11.73Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a68b232e5cd96b15c', 'RB-T128', 'feature', 'REA.Bloodlines', 57496, '2026-10-24T20:48:29.808Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a6f60f2a5687fef60', 'RB-T184', 'fix', 'REA.Bloodlines', 19999, '2026-10-24T21:32:33.237Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a7d53fd07cbe047c1', 'RB-T182', 'fix', 'REA.Bloodlines', 153872, '2026-10-24T20:25:06.821Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a863b443959826869', 'RB-T118', 'feature', 'REA.Bloodlines', 680611, '2026-10-24T21:15:16.846Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a89c35ef078e00f63', 'RB-T187', 'fix', 'REA.Bloodlines', 1821946, '2026-10-24T23:45:31.352Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-a8b739470f002c6e7', 'RB-T136', 'feature', 'REA.Bloodlines', 173268, '2026-10-24T22:46:56.84Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-aa0c068b419f780df', 'RB-T130', 'feature', 'REA.Bloodlines', 81337, '2026-10-24T20:30:54.178Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-aae4a0730b172141e', 'RB-T187', 'fix', 'REA.Bloodlines', 33172, '2026-10-24T21:33:22.801Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-ab59a8b73939d0745', 'RB-T134', 'feature', 'REA.Bloodlines', 20843, '2026-10-24T21:03:27.362Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-ad4ef338f614a6706', 'RB-T184', 'fix', 'REA.Bloodlines', 140698, '2026-10-24T20:34:36.952Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('3218d735-2bae-4de1-89d9-348ec56c423b/agent-af8caf2705f3337bd', 'RB-T121', 'feature', 'REA.Bloodlines', 165581, '2026-10-24T20:45:16.7Z', 0, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a00d1cfb2d31038e0', 'U.CHN-T59', 'feature', 'Unity.Chain', 20654, '2026-10-24T18:59:09.739Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a0cba8b88de85f5c3', 'U.CHN-T59', 'feature', 'Unity.Chain', 257401, '2026-10-24T19:43:01.264Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a0e0fa42a56e7b4df', 'UNI-T56', 'feature', 'Unity.Chain', 356294, '2026-10-24T16:55:42.2Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a0fc7757fd38ec7ce', 'UNI-T40', 'feature', 'Unity.Chain', 311636, '2026-10-24T18:06:28.757Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a12ff691c05ffe0d5', 'UNI-T19', 'fix', 'Unity.Chain', 477240, '2026-10-24T17:15:23.674Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a142ab3a00429231b', 'U.CHN-T59', 'feature', 'Unity.Chain', 111554, '2026-10-24T20:19:02.329Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a2056887c0410788f', 'U.CHN-T71', 'chore', 'Unity.Chain', 79440, '2026-10-24T19:06:25.873Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a250c7cdec067df1f', 'U.CHN-T45', 'chore', 'Unity.Chain', 19583, '2026-10-24T18:58:00.99Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a27d73e9487290e5b', 'U.CHN-T72', 'fix', 'Unity.Chain', 151384, '2026-10-24T19:43:59.659Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a30ef21b7f4b5e2e8', 'U.CHN-T26', 'feature', 'Unity.Chain', 706507, '2026-10-24T21:25:21.744Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a4ed6a09255a98f45', 'UNI-T20', 'fix', 'Unity.Chain', 342908, '2026-10-24T17:49:42.75Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a54eacac7f460966f', 'U.CHN-T59', 'feature', 'Unity.Chain', 21323, '2026-10-24T18:58:41.688Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a5b0cbbc7c0ef6431', 'U.CHN-T62', 'chore', 'Unity.Chain', 462427, '2026-10-24T22:09:18.069Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a5cc96ca4644c9acd', 'U.CHN-T61', 'feature', 'Unity.Chain', 298278, '2026-10-24T20:43:06.213Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a77189ec47b7e2658', 'UNI-T72', 'fix', 'Unity.Chain', 52123, '2026-10-24T18:43:48.01Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a8962482f63495ed8', 'U.CHN-T45', 'chore', 'Unity.Chain', 1529648, '2026-10-24T22:16:52.808Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-a968de8e620e8f46e', 'U.CHN-T60', 'feature', 'Unity.Chain', 160134, '2026-10-24T20:27:29.975Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-aa12b0796ea9fed9f', 'U.CHN-T45', 'chore', 'Unity.Chain', 332775, '2026-10-24T19:43:11.262Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-aa8637a8445a34daf', 'UNI-T58', 'feature', 'Unity.Chain', 150370, '2026-10-24T17:44:04.114Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-aaa34cbabc970417c', 'UNI-T71', 'chore', 'Unity.Chain', 20316, '2026-10-24T18:46:26.173Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-aaed37f5166e60b7d', 'U.CHN-T45', 'chore', 'Unity.Chain', 487332, '2026-10-24T20:46:00.958Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-ab2aca329d3db1003', 'U.CHN-T72', 'fix', 'Unity.Chain', 95980, '2026-10-24T19:03:06.512Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-ab6122afcb93f71f6', 'UNI-T41', 'feature', 'Unity.Chain', 223710, '2026-10-24T18:17:11.141Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-abed28cbfcbef017d', 'UNI-T70', 'fix', 'Unity.Chain', 114705, '2026-10-24T17:40:48.395Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-ac2ac890e826579d1', 'U.CHN-T71', 'chore', 'Unity.Chain', 140665, '2026-10-24T19:40:54.79Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-ac39f02a8b9919924', 'U.CHN-T73', 'feature', 'Unity.Chain', 223045, '2026-10-24T21:42:56.954Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-ad9d42c33b254bfe9', 'UNI-T43', 'feature', 'Unity.Chain', 172757, '2026-10-24T18:27:38.013Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-adc498df063fa5040', 'UNI-T57', 'feature', 'Unity.Chain', 191711, '2026-10-24T17:21:39.677Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-ae6e55eaad1062b2e', 'UNI-T45', 'chore', 'Unity.Chain', 257299, '2026-10-24T18:41:03.537Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('33e02961-5b42-446b-a988-ee46b3b0ee9d/agent-af955a547f7002cae', 'UNI-T59', 'feature', 'Unity.Chain', 81783, '2026-10-24T18:42:13.991Z', 0, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('363164a9-821e-47b4-beec-d46425505b94', 'U.INC-T35', 'chore', 'Unity.Incognito', 204384, '2026-10-26T04:43:13.179Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('37f0d253-9283-4cdb-a58c-222beaa2cde9', 'U.RICO-T36', 'chore', 'Unity.Ricochet', 38322, '2026-10-24T20:10:06.63Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('402a9c10-40a7-4bd1-9927-c2736d9f03a2', 'U.INC-T41', 'fix', 'Unity.Incognito', 22012, '2026-10-25T04:02:23.716Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40712f5d-8f9d-48a5-b221-1606785b76cb', 'UC-T41', 'fix', 'Unity.Cursory', 6192, '2026-10-25T06:31:43.658Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a09a19d9fb4802103', 'U.INC-T30', 'feature', 'Unity.Incognito', 212757, '2026-10-25T01:45:30.322Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a1187554741301946', 'U.INC-T31', 'feature', 'Unity.Incognito', 509583, '2026-10-25T02:16:11.111Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a15d187c303f5d032', 'U.INC-T41', 'fix', 'Unity.Incognito', 479713, '2026-10-25T04:13:48.239Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a324aa88c57a83d97', 'U.INC-T35', 'chore', 'Unity.Incognito', 220483, '2026-10-25T03:54:00.111Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a470f51eb207a3796', 'U.INC-T29', 'feature', 'Unity.Incognito', 229289, '2026-10-25T01:33:53.75Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a52e058467e05007d', 'U.INC-T33', 'chore', 'Unity.Incognito', 147614, '2026-10-25T03:00:08.268Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-a938c9d72127ed0b9', 'U.INC-T34', 'docs', 'Unity.Incognito', 147348, '2026-10-25T03:16:35.308Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40d95301-df3b-4ba7-82c1-d81c11082641/agent-afd765b93eeff7b61', 'U.INC-T32', 'feature', 'Unity.Incognito', 283609, '2026-10-25T02:44:58.697Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('40ff5059-9aea-4520-8101-7676cd5c231e', 'GG-T209', 'fix', 'Unity.GridGame', 16473, '2026-10-28T18:34:54.632Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a08239b8772d7d4b1', 'RB-T219', 'fix', 'REA.Bloodlines', 69378, '2026-10-27T22:14:16.942Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a09e90110200930b4', 'RB-T224', 'fix', 'REA.Bloodlines', 101053, '2026-10-27T21:11:17.777Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a107e0ee309844b50', 'RB-T225', 'fix', 'REA.Bloodlines', 149558, '2026-10-27T21:05:01.373Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a1d3631586c3d9d1a', 'RB-T223', 'fix', 'REA.Bloodlines', 305365, '2026-10-27T21:31:50.445Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a2136babadaa1d50f', 'RB-T242', 'feature', 'REA.Bloodlines', 138640, '2026-10-27T19:59:19.61Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a256fa8caf80abdf9', 'RB-T219', 'fix', 'REA.Bloodlines', 90860, '2026-10-27T21:16:32.36Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a2a49d833cdfaada5', 'RB-T229', 'fix', 'REA.Bloodlines', 44238, '2026-10-27T22:12:54.004Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a2b10d43b35d8e7ea', 'RB-T241', 'fix', 'REA.Bloodlines', 378682, '2026-10-27T20:32:28.541Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a3403846596b8361c', 'RB-T239', 'fix', 'REA.Bloodlines', 146242, '2026-10-27T20:48:00.379Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a421ab9ea41313d94', 'RB-T222', 'fix', 'REA.Bloodlines', 128362, '2026-10-27T21:24:07.414Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a4cafb789c49029ef', 'RB-T235', 'fix', 'REA.Bloodlines', 216118, '2026-10-27T20:55:34.814Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a4ed8723276894770', 'RB-T233', 'fix', 'REA.Bloodlines', 81884, '2026-10-27T20:36:12.745Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a5f6748b1c6568487', 'RB-T243', 'fix', 'REA.Bloodlines', 74090, '2026-10-27T19:57:39.072Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a6be74af77bf32949', 'RB-T220', 'fix', 'REA.Bloodlines', 72081, '2026-10-27T21:19:34.899Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a6d4d637616e88547', 'RB-T238', 'fix', 'REA.Bloodlines', 123239, '2026-10-27T20:40:12.998Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a84ceaaf6e2c99ba3', 'RB-T230', 'fix', 'REA.Bloodlines', 190080, '2026-10-27T20:45:04.773Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-a9b20660f781c11fd', 'RB-T232', 'fix', 'REA.Bloodlines', 80675, '2026-10-27T20:37:37.076Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-aa16b8e659b66ac76', 'RB-T229', 'fix', 'REA.Bloodlines', 97190, '2026-10-27T20:39:47.756Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-aa88d07fc0515b300', 'RB-T221', 'fix', 'REA.Bloodlines', 87832, '2026-10-27T21:15:54.177Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-ab705aa0653d3f587', 'RB-T227', 'fix', 'REA.Bloodlines', 62276, '2026-10-27T20:38:08.951Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-ab717c3e390115ea1', 'RB-T228', 'fix', 'REA.Bloodlines', 173632, '2026-10-27T20:49:29.101Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-ac121d024c277da36', 'RB-T236', 'fix', 'REA.Bloodlines', 197733, '2026-10-27T20:44:51.457Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-ac5a17e514aeec2d9', 'RB-T237', 'fix', 'REA.Bloodlines', 162567, '2026-10-27T20:49:13.511Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-aceb9eaa7da07dfab', 'RB-T240', 'fix', 'REA.Bloodlines', 105501, '2026-10-27T20:43:45.994Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-ad3ed88ed215bc02f', 'RB-T231', 'fix', 'REA.Bloodlines', 148253, '2026-10-27T20:48:41.937Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('45cf52c0-6fa6-40aa-8b74-97a3411645e5/agent-aeff2139c276033fd', 'RB-T226', 'fix', 'REA.Bloodlines', 83979, '2026-10-27T20:56:32.948Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4735b563-69e7-438d-b563-834a57645702', 'U.VAN-T6', 'chore', 'Unity.ProjectVania', 63779, '2026-10-24T10:32:23.106Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-a385401694d4da8a8', 'U.INC-T9', 'feature', 'Unity.Incognito', 281094, '2026-10-22T10:42:44.317Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-a5450f7e05ef6431b', 'U.INC-T10', 'feature', 'Unity.Incognito', 148870, '2026-10-22T10:26:12.033Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-a8c1dd18e823cf472', 'U.INC-T4', 'feature', 'Unity.Incognito', 96625, '2026-10-22T08:55:47.743Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-abf750e7431c9f8b7', 'U.INC-T8', 'feature', 'Unity.Incognito', 220455, '2026-10-22T10:33:11.04Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-abfdc61f3aafddb47', 'U.INC-T6', 'feature', 'Unity.Incognito', 219224, '2026-10-22T11:05:07.233Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('476f6f2a-73c7-4e9a-836e-969d74acef9d/agent-ad8684c5ce5db1c60', 'U.INC-T5', 'feature', 'Unity.Incognito', 275888, '2026-10-22T10:17:10.355Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('47cce885-698f-4812-8113-d348cf939e37', 'U.HARE-T45', 'chore', 'Unity.Hare', 41917, '2026-10-24T22:21:28.123Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('488587f4-0437-4b45-bf07-e5866af0dcd1/agent-a0273ec3cc019b9e8', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 19954, '2026-10-25T20:54:49.439Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('488587f4-0437-4b45-bf07-e5866af0dcd1/agent-a77728e4fbabe2c9e', 'M.ORCH-T817', 'fix', 'MindAttic.Orchestrator', 115322, '2026-10-25T20:52:48.213Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('488587f4-0437-4b45-bf07-e5866af0dcd1/agent-aef685c275e6baee6', 'M.ORCH-T813', 'fix', 'MindAttic.Orchestrator', 118993, '2026-10-25T20:54:13.645Z', 0, 'sonnet');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4a5dee41-849b-4116-9ee7-cf157fec93ea', 'U.RICO-T77', 'fix', 'Unity.Ricochet', 53357, '2026-10-26T08:10:38.269Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4acae942-2aeb-49cf-b3de-177d3021f7f8', 'U.VAN-T21', 'chore', 'Unity.ProjectVania', 19110, '2026-10-25T03:20:55.808Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4c3b85b1-1314-4760-8c2d-dc7646297287', 'GG-T18', 'feature', 'GridGameGaiden', 51661, '2026-10-15T09:03:05.17Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4c535ef7-aba4-4ad2-96db-fa46049ea274', 'U.CHN-T62', 'chore', 'Unity.Chain', 27820, '2026-10-26T04:24:26.171Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4e31197a-bbdf-45da-9680-649daa08074b', 'GG-T36', 'feature', 'GridGameGaiden', 493758, '2026-10-15T14:55:05.913Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a0b9cc9b462364bb0', 'UNI-T17', 'fix', 'Unity.Chain', 84516, '2026-10-23T10:34:48.588Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a0d7923d1756176d7', 'UNI-T8', 'feature', 'Unity.Chain', 118087, '2026-10-23T06:50:58.547Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a132278f9cb93e894', 'UNI-T16', 'fix', 'Unity.Chain', 285891, '2026-10-23T10:55:15.166Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a693b8e87902bd91f', 'UNI-T7', 'feature', 'Unity.Chain', 70828, '2026-10-23T05:38:16.835Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-a7d9fd7f9ee0c5d1a', 'UNI-T9', 'feature', 'Unity.Chain', 129302, '2026-10-23T07:20:20.584Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-aa3cde8df6b02423a', 'UNI-T10', 'feature', 'Unity.Chain', 288764, '2026-10-23T08:58:53.686Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-abb43334c33d29779', 'UNI-T6', 'feature', 'Unity.Chain', 114885, '2026-10-23T05:45:21.135Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('4ef11499-eaa9-4435-bc12-c068b0d0ba06/agent-ad474e7221b75f45f', 'UNI-T12', 'feature', 'Unity.Chain', 164853, '2026-10-23T09:38:28.172Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5062a6e7-5255-41ac-9ce1-e9b503db58e3', 'U.HARE-T70', 'chore', 'Unity.Hare', 19569, '2026-10-28T17:43:59.758Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('50b04d70-5d44-456c-91cd-ef5fe59673d4', 'GG-T40', 'feature', 'GridGameGaiden', 199068, '2026-10-15T14:08:41.696Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('51870e7d-1d97-4418-8b48-6097fb43ff7e', 'M.LNCH-T16', 'chore', 'MindAttic.Launcher', 25041, '2026-10-23T19:17:09.226Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('5210b6b4-485a-4a8d-86a9-e984d6ca859a', 'U.SPC-T585', 'fix', 'Unity.SpaceGod', 33086, '2026-10-25T05:34:53.094Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52728cb7-3bd9-4584-bd3c-85b160ecb8fc', 'MB-T2', 'chore', 'MediaButler', 89617, '2026-10-16T00:57:42.857Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a00f188a6bd92760a', 'PRS-T135', 'chore', 'Prose', 34782, '2026-10-27T07:34:40.602Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a0de088035a74dcc3', 'PRS-T309', 'fix', 'Prose', 116568, '2026-10-27T18:58:49.426Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a1d019ff479373734', 'PRS-T83', 'fix', 'Prose', 35867, '2026-10-27T07:46:31.969Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a1d40e58cd0c34a2f', 'PRS-T24', 'fix', 'Prose', 948245, '2026-10-27T16:59:23.905Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a291aadd972c5c442', 'PRS-T32', 'fix', 'Prose', 104617, '2026-10-27T07:53:32.205Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a29a9e72bc4e227dd', 'PRS-T314', 'fix', 'Prose', 118290, '2026-10-27T18:58:49.561Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a2fc93a72f5319698', 'PRS-T29', 'fix', 'Prose', 136755, '2026-10-27T08:03:42.848Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a307c79f28b900ebe', 'PRS-T31', 'fix', 'Prose', 97492, '2026-10-27T07:58:04.824Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a3258732122f98da7', 'PRS-T28', 'fix', 'Prose', 56619, '2026-10-27T08:12:26.793Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a33d1c58cfde8e863', 'PRS-T105', 'fix', 'Prose', 33607, '2026-10-27T08:22:51.149Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a385075141021c2f9', 'PRS-T104', 'chore', 'Prose', 27323, '2026-10-27T07:34:10.15Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a3d402c248a04f43e', 'PRS-T224', 'fix', 'Prose', 36259, '2026-10-27T06:31:26.104Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a49d4be2834c08213', 'PRS-T220', 'chore', 'Prose', 132613, '2026-10-27T07:29:30.486Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a4b3157793dd2f79f', 'PRS-T133', 'chore', 'Prose', 2707619, '2026-10-27T17:33:23.539Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a573ed45a4f8a10a6', 'PRS-T227', 'fix', 'Prose', 58075, '2026-10-27T06:33:41.607Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a70bc8ebdd24fdb99', 'PRS-T313', 'fix', 'Prose', 70675, '2026-10-27T18:58:50.106Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a729da8cac751aa36', 'PRS-T306', 'fix', 'Prose', 65206, '2026-10-27T18:52:24.843Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a949989549c49c6cb', 'PRS-T134', 'chore', 'Prose', 24159, '2026-10-27T07:31:25.766Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a96a1f21025b032a3', 'PRS-T187', 'chore', 'Prose', 49020, '2026-10-27T07:30:33.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a9a9c956404863f12', 'PRS-T133', 'chore', 'Prose', 947622, '2026-10-27T09:52:48.745Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-a9e7fb4da5eaccc33', 'PRS-T311', 'fix', 'Prose', 129968, '2026-10-27T18:54:25.685Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ab34d68c798d3ccc1', 'PRS-T307', 'fix', 'Prose', 71141, '2026-10-27T18:56:40.288Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ab68d9eb3271ab597', 'PRS-T105', 'fix', 'Prose', 62245, '2026-10-27T07:38:39.15Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ab6e3a54122d29c90', 'PRS-T133', 'chore', 'Prose', 239793, '2026-10-27T07:14:27.036Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ab7f827cdffbcb837', 'PRS-T305', 'fix', 'Prose', 43001, '2026-10-27T18:47:44.836Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ac09701a59a88e64a', 'PRS-T304', 'fix', 'Prose', 34588, '2026-10-27T18:58:49.537Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ac10e250d1445ab30', 'PRS-T217', 'docs', 'Prose', 107472, '2026-10-27T07:43:03.369Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ad1fe6919f661204e', 'PRS-T308', 'fix', 'Prose', 97742, '2026-10-27T18:55:02.883Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ae47a2818f64b2566', 'PRS-T20', 'fix', 'Prose', 58237, '2026-10-27T09:59:27.721Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-ae8dd1e816cd7bf08', 'PRS-T24', 'fix', 'Prose', 66980, '2026-10-27T10:08:26.615Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-aec90c10e902e63ed', 'PRS-T55', 'fix', 'Prose', 32451, '2026-10-27T07:47:33.173Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-aede37b5ea0faa2ed', 'PRS-T312', 'fix', 'Prose', 177511, '2026-10-27T18:58:50.059Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-af1b4d2840549ff92', 'PRS-T310', 'fix', 'Prose', 119169, '2026-10-27T18:54:01.753Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('52d32bc1-0939-4dbe-8425-35585a84452a/agent-af8ee945d3f43327e', 'PRS-T27', 'fix', 'Prose', 47936, '2026-10-27T08:34:22.02Z', 0, 'sonnet');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('55df4832-1937-491d-80ff-eba3211fc9cc', 'M.ORCH-T832', 'fix', 'MindAttic.Orchestrator', 7625, '2026-10-26T00:48:53.13Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('57bc4035-34b0-4460-8224-8f3899140d3e', 'PRS-T135', 'chore', 'Prose', 106640, '2026-10-27T07:34:30.682Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('688eedc3-c0f3-4974-bb00-919e01bb132b', 'U.CHN-T72', 'fix', 'Unity.Chain', 55769, '2026-10-24T19:39:07.286Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('6a865ca1-9184-490e-82df-7e0e0b533905', 'U.SPC-T542', 'fix', 'Unity.SpaceGod', 46907, '2026-10-25T03:56:02.165Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('73d1de57-f783-4b73-b97b-dbf54966e5c1', 'M.ORCH-T1045', 'fix', 'MindAttic.Orchestrator', 24414, '2026-10-26T19:14:54.157Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('755321b6-0d26-42da-ab73-4b48f9dc6828', 'M.ORCH-T801', 'fix', 'MindAttic.Orchestrator', 7859, '2026-10-24T18:02:28.099Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('760f5c10-dbcb-47c4-a2e2-221b717d8bbd', 'U.CHN-T63', 'chore', 'Unity.Chain', 54389, '2026-10-26T04:43:04.465Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('76d54fd4-c346-437c-936d-f61f475d1d9c', 'GG-T44', 'feature', 'GridGameGaiden', 233632, '2026-10-15T15:14:17.606Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('777043ff-2435-4c23-b5eb-f9dc26712a00', 'U.SPC-T31', 'chore', 'UnitySolarSystem', 147687, '2026-10-15T10:15:29.572Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('7838b3d2-863c-414f-95e7-eaf3bb4b4ba0', 'SS-T77', 'chore', 'Prose', 1555326, '2026-10-15T20:48:04.151Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('78678a8f-e6d6-4bd4-8542-89a27cf57d61', 'U.CHN-T88', 'fix', 'Unity.Chain', 35957, '2026-10-28T15:33:56.909Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8268caae-5412-4db7-b777-f70311d92edf', 'U.RICO-T28', 'fix', 'Unity.Ricochet', 21654, '2026-10-26T06:22:19.91Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('867068c6-bd5e-4c30-89c1-fb30b9014293', 'U.VAN-T20', 'chore', 'Unity.ProjectVania', 71327, '2026-10-25T02:43:46.41Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('86751808-1e40-4c36-907f-7c47830c38fc', 'U.RICO-T96', 'fix', 'Unity.Ricochet', 20377, '2026-10-28T14:25:25.07Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('86ffa201-97a3-4afd-a477-f6b526d2c80d', 'M.ORCH-T864', 'fix', 'MindAttic.Orchestrator', 46458, '2026-10-26T02:40:51.47Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('872823a1-081b-473a-aa3f-1e511c29d980', 'GG-T202', 'fix', 'Unity.GridGame', 6260, '2026-10-25T07:11:20.461Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8ccdd621-b434-4802-a93c-7e0a59ac3bb0', 'M.ORCH-T896', 'fix', 'MindAttic.Orchestrator', 26950, '2026-10-26T04:31:27.936Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8cfb5fdd-a868-49e0-bb37-fb493a29aefa/agent-a2d208eafa7d30ec7', 'UQD-T10', 'docs', 'Unity.QuickDuel', 98085, '2026-10-22T07:47:26.685Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('8cfb5fdd-a868-49e0-bb37-fb493a29aefa/agent-a40bfa1a3186a2e69', 'UQD-T17', 'fix', 'Unity.QuickDuel', 40834, '2026-10-22T09:01:55.927Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('904dc1ee-6cdb-474c-a991-ea9fcbdac6a1', 'U.VAN-T19', 'chore', 'Unity.ProjectVania', 220603, '2026-10-25T01:55:15.617Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9066c252-4e61-4adc-8e1b-61120f513162', 'SS-T56', 'chore', 'Prose', 309300, '2026-10-15T13:05:39.825Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('90be2624-ab61-45bb-8010-60c3ad5fbfcf/agent-a00e1e1836e715677', 'U.HARE-T111', 'fix', 'Unity.Rea.Hare', 132116, '2026-10-30T05:31:23.778Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('90be2624-ab61-45bb-8010-60c3ad5fbfcf/agent-a1961e61109a5f729', 'U.HARE-T110', 'fix', 'Unity.Rea.Hare', 216602, '2026-10-30T05:33:24.94Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('90be2624-ab61-45bb-8010-60c3ad5fbfcf/agent-a523b9cf52cc5e0b5', 'U.HARE-T109', 'fix', 'Unity.Rea.Hare', 100358, '2026-10-30T05:30:36.969Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('90be2624-ab61-45bb-8010-60c3ad5fbfcf/agent-a9755ff557fbed4e3', 'U.HARE-T112', 'fix', 'Unity.Rea.Hare', 129329, '2026-10-30T05:26:38.541Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('90be2624-ab61-45bb-8010-60c3ad5fbfcf/agent-ab1ed3e6a9bfab3a2', 'U.HARE-T33', 'feature', 'Unity.Rea.Hare', 152964, '2026-10-30T05:21:37.754Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9173831a-1a8b-45e5-ab65-3105b64e8e81', 'GG-T28', 'chore', 'GridGameGaiden', 96220, '2026-10-15T11:11:55.835Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9195166d-2df1-407f-a3d0-eb0ced7bb910', 'AGP-T3', 'docs', 'AutoGamePlay', 24868, '2026-10-28T06:41:26.38Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('958ed843-01cc-4dae-baf8-5e5ab2af8c7b', 'U.RICO-T37', 'chore', 'Unity.Ricochet', 52620, '2026-10-26T04:24:43.429Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a0c7d261f5ccc6d77', 'AGP-T11', 'feature', 'REA.Bloodlines', 128074, '2026-10-28T14:57:37.445Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a0fbadc0e4e08efc2', 'AGP-T13', 'feature', 'REA.Bloodlines', 237720, '2026-10-28T15:46:54.069Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a135ec6e65abc40f2', 'AGP-T7', 'feature', 'REA.Bloodlines', 291765, '2026-10-28T15:22:05.833Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a24b041fd2861f6ab', 'RB-T262', 'feature', 'REA.Bloodlines', 221474, '2026-10-28T16:01:00.639Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a25193e20f9643ef8', 'AGP-T19', 'fix', 'REA.Bloodlines', 183039, '2026-10-28T15:33:23.554Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a2534ebd2b694011e', 'AGP-T21', 'fix', 'REA.Bloodlines', 83317, '2026-10-28T15:36:36.689Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a2cb41b0c2eec05ef', 'RB-T276', 'fix', 'REA.Bloodlines', 366496, '2026-10-28T17:43:49.563Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a2e27975d4d532de2', 'RB-T280', 'fix', 'REA.Bloodlines', 103009, '2026-10-28T17:16:16.81Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a337368d14d07b9a3', 'AGP-T9', 'feature', 'REA.Bloodlines', 215364, '2026-10-28T15:18:51.236Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a337c9698e1fdc837', 'AGP-T22', 'fix', 'REA.Bloodlines', 29755, '2026-10-28T15:33:12.101Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a3784b063fb0c785f', 'AGP-T10', 'feature', 'REA.Bloodlines', 154651, '2026-10-28T15:05:49.802Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a382f443cb6ea3b54', 'RB-T260', 'feature', 'REA.Bloodlines', 205744, '2026-10-28T16:35:44.034Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a46ef78f8cdbdb2bb', 'RB-T279', 'fix', 'REA.Bloodlines', 191210, '2026-10-28T17:26:16.494Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a47dbebe058960f7f', 'RB-T257', 'feature', 'REA.Bloodlines', 32876, '2026-10-28T16:29:36.746Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a53ca62854851299b', 'AGP-T24', 'fix', 'REA.Bloodlines', 181595, '2026-10-28T17:23:33.265Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a53f4b64501e5c82a', 'AGP-T8', 'feature', 'REA.Bloodlines', 233003, '2026-10-28T15:18:04.3Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a63b636290f0195dc', 'AGP-T23', 'fix', 'REA.Bloodlines', 186678, '2026-10-28T17:20:16.53Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a685d705daa2a3b1a', 'RB-T258', 'feature', 'REA.Bloodlines', 272692, '2026-10-28T16:41:48.846Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a71f6b24e82de42e4', 'AGP-T14', 'feature', 'REA.Bloodlines', 413301, '2026-10-28T17:36:27.684Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a772137de5fe3fb7c', 'RB-T264', 'feature', 'REA.Bloodlines', 209124, '2026-10-28T14:34:45.02Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a786f94fa2e26b3a7', 'AGP-T6', 'feature', 'REA.Bloodlines', 544699, '2026-10-28T16:27:12.885Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a7d80013f0d01f942', 'RB-T259', 'feature', 'REA.Bloodlines', 289164, '2026-10-28T16:53:22.855Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a7d9cf1afadcb52ee', 'AGP-T18', 'fix', 'REA.Bloodlines', 199955, '2026-10-28T14:45:33.803Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a9476ca0e43eac610', 'RB-T256', 'feature', 'REA.Bloodlines', 152975, '2026-10-28T17:09:56.921Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-a967542e2c234638d', 'RB-T246', 'feature', 'REA.Bloodlines', 138747, '2026-10-28T14:48:51.048Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-aaa52d98e900285c7', 'AGP-T25', 'fix', 'REA.Bloodlines', 82316, '2026-10-28T17:15:54.678Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-aac0dd708897ab7fe', 'RB-T273', 'feature', 'REA.Bloodlines', 128217, '2026-10-28T15:38:57.462Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-ac1bf00c26bbfdeb0', 'AGP-T5', 'feature', 'REA.Bloodlines', 487978, '2026-10-28T15:59:08.003Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-acc60b87695dcedf5', 'RB-T234', 'feature', 'REA.Bloodlines', 335977, '2026-10-28T14:49:28.987Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-acf31023b97fa876a', 'RB-T277', 'fix', 'REA.Bloodlines', 87535, '2026-10-28T17:17:19.661Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-ad050e4066d067bbc', 'AGP-T4', 'feature', 'REA.Bloodlines', 252032, '2026-10-28T14:50:46.081Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-ad75482732c76559a', 'RB-T261', 'feature', 'REA.Bloodlines', 348354, '2026-10-28T16:57:38.758Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-ad9e581ea272d89f2', 'RB-T278', 'fix', 'REA.Bloodlines', 137524, '2026-10-28T17:23:23.855Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-adb169b5b9d0a599d', 'AGP-T12', 'feature', 'REA.Bloodlines', 432423, '2026-10-28T16:49:43.734Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a09255a-43ca-4cfa-84d8-258b4a81afb4/agent-ae1571e8d9c043574', 'AGP-T20', 'fix', 'REA.Bloodlines', 130460, '2026-10-28T15:31:17.592Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a1ad58b-3d01-4256-8b5d-198ddfc7c911', 'M.LOGS-T12', 'docs', 'MindAttic.Log', 15478, '2026-10-21T18:10:56.224Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a45ef64-29c3-43a3-95b0-479a222265c7', 'U.INC-T22', 'chore', 'Unity.Incognito', 60457, '2026-10-24T12:03:24.529Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9a851808-0c2d-48ed-8e81-75b916af6fb1', 'SS-T22', 'chore', 'Prose', 36102, '2026-10-15T11:56:51.712Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9b3c855c-42f8-4b01-8239-cf0d7f18e463', 'U.SPC-T529', 'fix', 'Unity.SpaceGod', 5638, '2026-10-24T19:11:06.237Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('9b4c0d3b-c94d-4ff1-95ee-9a7ebef43bf6', 'U.HARE-T42', 'chore', 'Unity.Hare', 51184, '2026-10-24T20:25:40.012Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a07cd0cd357aca056', 'U.CHN-T76', 'chore', '(workspace root)', 99051, '2026-10-26T17:07:25.003Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a0daa33bf678d0877', 'U.OVW-T23', 'feature', '(workspace root)', 108952, '2026-10-26T19:42:15.129Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a26429dff95786fed', 'U.RICO-T52', 'feature', '(workspace root)', 218206, '2026-10-26T22:04:38.126Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a480c99d414c6c1ec', 'U.HARE-T87', 'fix', '(workspace root)', 100971, '2026-10-26T22:05:08.944Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a52c8fa88422afe7e', 'U.RICO-T79', 'feature', '(workspace root)', 312570, '2026-10-26T17:29:06.927Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a5c67f3c6b6de015b', 'U.RICO-T80', 'fix', '(workspace root)', 172776, '2026-10-26T22:04:43.669Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a6163673e094ede64', 'U.OVW-T24', 'feature', '(workspace root)', 355672, '2026-10-26T22:05:01.153Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a6ec8f58dde9995df', 'U.RICO-T52', 'feature', '(workspace root)', 238215, '2026-10-26T19:44:07.805Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a7b1b5ba418dca3dd', 'U.HARE-T19', 'feature', '(workspace root)', 244689, '2026-10-26T19:44:00.638Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a8a2f4a381e53813b', 'U.RICO-T86', 'fix', '(workspace root)', 86322, '2026-10-26T19:46:14.088Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a969dbe3d51dd2009', 'U.RICO-T78', 'feature', '(workspace root)', 471462, '2026-10-26T17:34:55.982Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a9a7bb08d3f82aedc', 'U.OVW-T44', 'fix', '(workspace root)', 33630, '2026-10-26T21:58:25.994Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-a9d5e8d41b38461e7', 'U.HARE-T19', 'feature', '(workspace root)', 137772, '2026-10-26T18:11:53.922Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-aa1871fe589873409', 'U.RICO-T84', 'fix', '(workspace root)', 244239, '2026-10-26T19:45:41.859Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-aa6afa6435f47e733', 'U.RICO-T52', 'feature', '(workspace root)', 40793, '2026-10-26T18:12:47.446Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-ab151c7bf67ee9283', 'U.OVW-T23', 'feature', '(workspace root)', 1523224, '2026-10-26T18:06:35.381Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-ab9c65065b5f3e60d', 'U.RICO-T84', 'fix', '(workspace root)', 166359, '2026-10-26T22:04:36.057Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-abb62ebea40f79178', 'UM-T76', 'fix', '(workspace root)', 55281, '2026-10-26T17:49:10.467Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-adecb7d019c5378d2', 'U.RICO-T51', 'feature', '(workspace root)', 274635, '2026-10-26T17:24:27.79Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-ae12ab8cf0bab0673', 'UM-T82', 'fix', '(workspace root)', 25557, '2026-10-26T17:49:39.62Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-af52a5aee62c61ded', 'U.HARE-T19', 'feature', '(workspace root)', 242136, '2026-10-26T22:04:32.899Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a1460c92-572a-4fdb-a95f-25ed871d6bdd/agent-afd24bc9b5302c4fa', 'U.HARE-T28', 'feature', '(workspace root)', 401443, '2026-10-26T17:26:26.472Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a161be04-0cf5-4976-a312-431ff54cfc4d', 'GG-T28', 'chore', 'GridGameGaiden', 116043, '2026-10-16T01:47:39.865Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a2590332-8249-4baa-835a-0e2036031e70', 'M.ORCH-T847', 'chore', 'MindAttic.Orchestrator', 42578, '2026-10-26T03:01:08.007Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a5fb6ccb-d3f6-438f-a6b5-01f0b3b57e11/agent-a0b8cf9e2737c866a', 'RB-T234', 'feature', 'REA.Bloodlines', 71276, '2026-10-28T04:01:47.133Z', 0, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('a6e2ca3a-7e78-4fc7-a9ce-b74ba69680c9', 'U.SPC-T538', 'fix', 'Unity.SpaceGod', 173319, '2026-10-25T03:38:43.219Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ab0e3299-5400-464f-a41e-0ce57d05399b', 'PRS-T133', 'chore', 'Prose', 76594, '2026-10-27T17:01:19.538Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b3f56723-429e-4917-ac64-fb061cc9a2f7', 'U.RICO-T38', 'chore', 'Unity.Ricochet', 35753, '2026-10-26T05:08:14.431Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b4b53ca8-20ef-43fa-8a06-0410b550846b', 'USS-T38', 'feature', 'UnitySolarSystem', 332571, '2026-10-15T11:15:54.182Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b6555e92-208c-40e1-97f9-9c2a416e26cb', 'GG-T21', 'chore', 'GridGameGaiden', 236676, '2026-10-16T02:25:11.71Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b69654b3-c0f4-4aee-be68-c271092bdc77', 'AUT-T8', 'chore', 'Automata', 157577, '2026-10-16T00:37:04.422Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b6fdf093-f759-4e26-9ada-b18103940feb', 'SS-T39', 'chore', 'Prose', 248565, '2026-10-15T13:00:53.667Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b71d1b8e-1724-4424-9fbd-b4f425f4eaf9', 'GG-T54', 'feature', 'GridGameGaiden', 213962, '2026-10-15T16:10:21.615Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a0017260b9e3ea32e', 'RTB-T205', 'chore', 'RogueTraderBrevity', 84363, '2026-10-25T10:32:45.286Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a01d68d0348a3171e', 'RTB-T198', 'chore', 'RogueTraderBrevity', 129285, '2026-10-25T10:22:09.308Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a040738332d1c5e4f', 'RTB-T118', 'chore', 'RogueTraderBrevity', 107677, '2026-10-25T07:45:50.467Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a05c87f15e4cc03a6', 'RTB-T140', 'chore', 'RogueTraderBrevity', 103984, '2026-10-25T08:17:03.421Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a0725b840bdc97c6e', 'RTB-T165', 'chore', 'RogueTraderBrevity', 107342, '2026-10-25T08:59:24.176Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a073b2400d2a5b99d', 'RTB-T155', 'chore', 'RogueTraderBrevity', 75638, '2026-10-25T08:45:49.565Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a07e0e66679336f3b', 'RTB-T200', 'chore', 'RogueTraderBrevity', 83325, '2026-10-25T10:31:54.34Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a098e1ae16b19b2d3', 'RTB-T216', 'chore', 'RogueTraderBrevity', 85993, '2026-10-25T11:04:01.522Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a0af189530572b400', 'RTB-T128', 'chore', 'RogueTraderBrevity', 123212, '2026-10-25T08:02:52.762Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a0af55f5f7c782121', 'RTB-T132', 'chore', 'RogueTraderBrevity', 95056, '2026-10-25T08:03:46.245Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a0f4e7a59402000f3', 'RTB-T163', 'chore', 'RogueTraderBrevity', 124013, '2026-10-25T08:59:38.892Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a0f8e4f8285214045', 'RTB-T182', 'chore', 'RogueTraderBrevity', 97461, '2026-10-25T09:59:05.162Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a105f8021b18cd79e', 'RTB-T143', 'chore', 'RogueTraderBrevity', 64714, '2026-10-25T08:17:42.46Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a12d0ed529f3a2617', 'RTB-T233', 'chore', 'RogueTraderBrevity', 50936, '2026-10-25T11:48:29.195Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a12ddd77531ce5a38', 'RTB-T172', 'chore', 'RogueTraderBrevity', 114645, '2026-10-25T09:12:51.222Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a1647a35955f71d5a', 'RTB-T126', 'chore', 'RogueTraderBrevity', 36762, '2026-10-25T07:48:27.753Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a16a7ee8036b997b0', 'RTB-T188', 'chore', 'RogueTraderBrevity', 81716, '2026-10-25T10:00:14.392Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a173d6c0a39994383', 'RTB-T185', 'chore', 'RogueTraderBrevity', 63974, '2026-10-25T09:58:51.704Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a1a41f37b46458586', 'RTB-T207', 'chore', 'RogueTraderBrevity', 103905, '2026-10-25T10:34:04.348Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a1b1d5da51d8fc710', 'RTB-T217', 'chore', 'RogueTraderBrevity', 51013, '2026-10-25T11:06:21.143Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a1c33ff773e0f7605', 'RTB-T212', 'chore', 'RogueTraderBrevity', 96255, '2026-10-25T10:59:58.67Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a1e5140619b53e2d4', 'RTB-T206', 'chore', 'RogueTraderBrevity', 105194, '2026-10-25T10:33:57.469Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a20a1d257da4440a4', 'RTB-T231', 'chore', 'RogueTraderBrevity', 58422, '2026-10-25T11:46:27.154Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a24a70b66873d195a', 'RTB-T239', 'chore', 'RogueTraderBrevity', 74767, '2026-10-25T11:56:57.712Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a2579f000c2f2dc23', 'RTB-T175', 'chore', 'RogueTraderBrevity', 90495, '2026-10-25T09:20:00.834Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a25d88dd9af98aeb7', 'RTB-T114', 'chore', 'RogueTraderBrevity', 118005, '2026-10-25T07:41:08.126Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a28aae58e61b6ca88', 'RTB-T146', 'chore', 'RogueTraderBrevity', 69473, '2026-10-25T08:29:35.383Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a29f335a62406bce8', 'RTB-T125', 'chore', 'RogueTraderBrevity', 32395, '2026-10-25T08:23:56.104Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a2c5e76b56fe86404', 'RTB-T169', 'chore', 'RogueTraderBrevity', 87243, '2026-10-25T09:07:44.941Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a2db8fbd1a2a56f71', 'RTB-T222', 'chore', 'RogueTraderBrevity', 91560, '2026-10-25T11:17:04.966Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a2fb67a62372e9b32', 'RTB-T129', 'chore', 'RogueTraderBrevity', 89240, '2026-10-25T08:02:42.396Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a315f7dacc1674e7c', 'RTB-T191', 'chore', 'RogueTraderBrevity', 112016, '2026-10-25T10:01:28.394Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a39e014998692ae60', 'RTB-T246', 'chore', 'RogueTraderBrevity', 114020, '2026-10-25T12:22:18.96Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a3a073e94898e1099', 'RTB-T166', 'chore', 'RogueTraderBrevity', 93397, '2026-10-25T09:03:37.418Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a3adbdbfb00388946', 'RTB-T213', 'chore', 'RogueTraderBrevity', 93233, '2026-10-25T11:03:50.705Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a3e5a2bd6ffcd9706', 'RTB-T184', 'chore', 'RogueTraderBrevity', 78899, '2026-10-25T09:59:24.295Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a3e864511feb11335', 'RTB-T152', 'chore', 'RogueTraderBrevity', 85276, '2026-10-25T08:39:17.224Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a3ff9555dcf54f0a5', 'RTB-T218', 'chore', 'RogueTraderBrevity', 104421, '2026-10-25T11:12:59.241Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a425db2849d5b6eef', 'RTB-T148', 'chore', 'RogueTraderBrevity', 87183, '2026-10-25T08:33:19.56Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a42bd73377f26233c', 'RTB-T137', 'chore', 'RogueTraderBrevity', 18813, '2026-10-25T08:04:42.558Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a443b2677ce932b5f', 'RTB-T171', 'chore', 'RogueTraderBrevity', 95771, '2026-10-25T09:11:13.481Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a46f8517b5a26b2a7', 'RTB-T170', 'chore', 'RogueTraderBrevity', 70992, '2026-10-25T09:09:14.853Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a473656c28838706e', 'RTB-T133', 'chore', 'RogueTraderBrevity', 107863, '2026-10-25T08:04:24.862Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a4bc04c3ca4464ad6', 'RTB-T225', 'chore', 'RogueTraderBrevity', 95148, '2026-10-25T11:27:20.476Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a4e5276092837de0e', 'RTB-T215', 'chore', 'RogueTraderBrevity', 97118, '2026-10-25T11:04:38.795Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a4f84288b00514147', 'RTB-T189', 'chore', 'RogueTraderBrevity', 47261, '2026-10-25T09:58:24.263Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a531802f34354d966', 'RTB-T204', 'chore', 'RogueTraderBrevity', 86244, '2026-10-25T10:32:34.025Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a5c1d3531935f78a0', 'RTB-T161', 'chore', 'RogueTraderBrevity', 90572, '2026-10-25T08:54:39.744Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a5c37f6ac7aa9f77a', 'RTB-T149', 'chore', 'RogueTraderBrevity', 81144, '2026-10-25T08:33:22.891Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a5dd67f310cd2e284', 'RTB-T159', 'chore', 'RogueTraderBrevity', 106822, '2026-10-25T08:50:45.472Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a5e0a9a0b7775f9ae', 'RTB-T179', 'chore', 'RogueTraderBrevity', 58615, '2026-10-25T09:24:11.181Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a5e7ddf09a60d2c48', 'RTB-T214', 'chore', 'RogueTraderBrevity', 102347, '2026-10-25T11:04:32.523Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a617fc613f09c91a5', 'RTB-T187', 'chore', 'RogueTraderBrevity', 95742, '2026-10-25T10:00:47.975Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a654466189dc0dc5f', 'RTB-T142', 'chore', 'RogueTraderBrevity', 72166, '2026-10-25T08:16:30.881Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a661dd847eb92a7f9', 'RTB-T192', 'chore', 'RogueTraderBrevity', 87207, '2026-10-25T10:00:41.793Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a6943a769d8fa2c4f', 'RTB-T145', 'chore', 'RogueTraderBrevity', 82434, '2026-10-25T08:19:28.98Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a73451379506674a4', 'RTB-T123', 'chore', 'RogueTraderBrevity', 79294, '2026-10-25T07:45:27.155Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a761f995288e8c8f0', 'RTB-T244', 'chore', 'RogueTraderBrevity', 131745, '2026-10-25T12:16:52.777Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a765b5f0a35f29cf3', 'RTB-T193', 'chore', 'RogueTraderBrevity', 96976, '2026-10-25T10:01:23.809Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a78a80c640da46e58', 'RTB-T195', 'chore', 'RogueTraderBrevity', 70563, '2026-10-25T10:04:44.757Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a7a44c914044c5d9e', 'RTB-T181', 'chore', 'RogueTraderBrevity', 83951, '2026-10-25T09:29:33.127Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a7b36b03301707802', 'RTB-T136', 'chore', 'RogueTraderBrevity', 86406, '2026-10-25T08:03:59.317Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a7c4abfbddbfc7d72', 'RTB-T190', 'chore', 'RogueTraderBrevity', 84490, '2026-10-25T09:59:56.395Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a7cde5fe461f94254', 'RTB-T219', 'chore', 'RogueTraderBrevity', 92074, '2026-10-25T11:16:46.986Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a80112738c64abb5a', 'RTB-T199', 'chore', 'RogueTraderBrevity', 98300, '2026-10-25T10:26:20.337Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a8014d5f9cba1b945', 'RTB-T197', 'chore', 'RogueTraderBrevity', 73512, '2026-10-25T10:14:05.064Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a82b3523a7eeeb963', 'RTB-T194', 'chore', 'RogueTraderBrevity', 96348, '2026-10-25T10:01:37.96Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a85222a26524d70f7', 'RTB-T210', 'chore', 'RogueTraderBrevity', 91298, '2026-10-25T10:52:23.327Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a853d71e6d2c062ea', 'RTB-T124', 'chore', 'RogueTraderBrevity', 67031, '2026-10-25T07:45:02.116Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a8545b7e0a3b79a0e', 'RTB-T153', 'chore', 'RogueTraderBrevity', 72268, '2026-10-25T08:40:31.194Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a884475a90f55c7a2', 'RTB-T131', 'chore', 'RogueTraderBrevity', 72352, '2026-10-25T08:02:05.9Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a8946f57bc58f6f18', 'RTB-T127', 'chore', 'RogueTraderBrevity', 84738, '2026-10-25T08:29:37.296Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a8a0fdf8ac14df879', 'RTB-T134', 'chore', 'RogueTraderBrevity', 77454, '2026-10-25T08:03:21.838Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a8bb8a1e6deb5a13a', 'RTB-T240', 'chore', 'RogueTraderBrevity', 110762, '2026-10-25T12:02:02.837Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a8e43870f17f52d40', 'RTB-T141', 'chore', 'RogueTraderBrevity', 73434, '2026-10-25T08:15:58.632Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a948d01a5f1c8a55f', 'RTB-T167', 'chore', 'RogueTraderBrevity', 81868, '2026-10-25T09:06:58.04Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a94df6a201071de9c', 'RTB-T237', 'chore', 'RogueTraderBrevity', 70198, '2026-10-25T11:54:05.39Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a967466ba3394c747', 'RTB-T196', 'chore', 'RogueTraderBrevity', 137816, '2026-10-25T10:11:03.422Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a99b6895361ec3175', 'RTB-T154', 'chore', 'RogueTraderBrevity', 95849, '2026-10-25T08:44:01.67Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a9afce7c25ea8fbdb', 'RTB-T245', 'chore', 'RogueTraderBrevity', 111178, '2026-10-25T12:21:44.664Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a9ba2354cbb8468ac', 'RTB-T122', 'chore', 'RogueTraderBrevity', 91139, '2026-10-25T07:45:54.465Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a9d11f04e0e35c3f7', 'RTB-T183', 'chore', 'RogueTraderBrevity', 92466, '2026-10-25T09:59:53.512Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a9dd5638f93c3b4db', 'RTB-T221', 'chore', 'RogueTraderBrevity', 112437, '2026-10-25T11:18:10.562Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-a9edda8c1ef4ccc7a', 'RTB-T125', 'chore', 'RogueTraderBrevity', 105761, '2026-10-25T07:47:15.235Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aa46cf9e0fdddd7c4', 'RTB-T242', 'chore', 'RogueTraderBrevity', 105096, '2026-10-25T12:08:35.034Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aa7f378df1fd2b6f6', 'RTB-T121', 'chore', 'RogueTraderBrevity', 130809, '2026-10-25T07:47:20.668Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aa88f20e29718eb3b', 'RTB-T180', 'chore', 'RogueTraderBrevity', 82546, '2026-10-25T09:26:04.074Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aa967b6601331804f', 'RTB-T127', 'chore', 'RogueTraderBrevity', 41514, '2026-10-25T07:50:00.535Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aaaad9564c9f49a89', 'RTB-T211', 'chore', 'RogueTraderBrevity', 88740, '2026-10-25T10:56:07.079Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aab36873b601f3168', 'RTB-T223', 'chore', 'RogueTraderBrevity', 87935, '2026-10-25T11:17:17.083Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aaca982bcad8d8496', 'RTB-T176', 'chore', 'RogueTraderBrevity', 96332, '2026-10-25T09:22:58.173Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ab128ad88be91aca5', 'RTB-T228', 'chore', 'RogueTraderBrevity', 87504, '2026-10-25T11:37:28.329Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ab1ef69a301ee1b37', 'RTB-T220', 'chore', 'RogueTraderBrevity', 107158, '2026-10-25T11:17:55.302Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ab324540f11bfaa78', 'RTB-T160', 'chore', 'RogueTraderBrevity', 79570, '2026-10-25T08:49:18.937Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ab4ae387a8d6427e5', 'RTB-T209', 'chore', 'RogueTraderBrevity', 86236, '2026-10-25T10:43:26.973Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ab4b653cf62e797dc', 'RTB-T208', 'chore', 'RogueTraderBrevity', 78774, '2026-10-25T10:37:25.631Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ab51a161ae254efb1', 'RTB-T117', 'chore', 'RogueTraderBrevity', 70547, '2026-10-25T07:43:38.72Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-abb9b74b2f9980939', 'RTB-T150', 'chore', 'RogueTraderBrevity', 109900, '2026-10-25T08:35:01.896Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-abd3145d3b42c5ca6', 'RTB-T177', 'chore', 'RogueTraderBrevity', 70666, '2026-10-25T09:21:55.009Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-abf2610632e958d3b', 'RTB-T156', 'chore', 'RogueTraderBrevity', 79344, '2026-10-25T08:45:55.191Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ac173034219a79b86', 'RTB-T236', 'chore', 'RogueTraderBrevity', 44098, '2026-10-25T11:51:14.969Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ac1d960d0ba3a4839', 'RTB-T130', 'chore', 'RogueTraderBrevity', 98741, '2026-10-25T08:02:47.026Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ac3552383eaefbceb', 'RTB-T126', 'chore', 'RogueTraderBrevity', 79840, '2026-10-25T08:27:16.39Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ac37322fa6d771ae6', 'RTB-T235', 'chore', 'RogueTraderBrevity', 50080, '2026-10-25T11:49:32.994Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ac3843ee871b7f272', 'RTB-T226', 'chore', 'RogueTraderBrevity', 82090, '2026-10-25T11:31:01.999Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-acb9df1b5d7710644', 'RTB-T203', 'chore', 'RogueTraderBrevity', 94377, '2026-10-25T10:32:55.253Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-acbb8d00aca8385c6', 'RTB-T178', 'chore', 'RogueTraderBrevity', 93264, '2026-10-25T09:24:05.528Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-acc52dd0d9efbf2f8', 'RTB-T232', 'chore', 'RogueTraderBrevity', 53209, '2026-10-25T11:48:31.774Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-accb2ff16d35d1249', 'RTB-T158', 'chore', 'RogueTraderBrevity', 83486, '2026-10-25T08:49:03.359Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-acff13f51ae879752', 'RTB-T201', 'chore', 'RogueTraderBrevity', 57886, '2026-10-25T10:31:00.833Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad0fa0795a37ef1a2', 'RTB-T224', 'chore', 'RogueTraderBrevity', 103148, '2026-10-25T11:23:17.659Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad358bd04e03ff7a3', 'RTB-T139', 'chore', 'RogueTraderBrevity', 77630, '2026-10-25T08:14:59.317Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad52afce775fae859', 'RTB-T120', 'chore', 'RogueTraderBrevity', 97036, '2026-10-25T07:45:34.461Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad535d9f10e4b2b83', 'RTB-T234', 'chore', 'RogueTraderBrevity', 57052, '2026-10-25T11:49:09.814Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad68277970bdb372c', 'RTB-T116', 'chore', 'RogueTraderBrevity', 106112, '2026-10-25T07:44:34.908Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad8450d86ebe03a91', 'RTB-T121', 'chore', 'RogueTraderBrevity', 79276, '2026-10-25T08:23:03.433Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ad9cc0783be4d470f', 'RTB-T147', 'chore', 'RogueTraderBrevity', 79992, '2026-10-25T08:30:07.068Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ada087ec8d7e3e0b8', 'RTB-T230', 'chore', 'RogueTraderBrevity', 88351, '2026-10-25T11:47:46.721Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ada3b82a2a0d2ef3b', 'RTB-T241', 'chore', 'RogueTraderBrevity', 105894, '2026-10-25T12:04:18.82Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-adaa1be7427bbabc9', 'RTB-T137', 'chore', 'RogueTraderBrevity', 77247, '2026-10-25T08:37:53.804Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-adbc8dcaae57088e7', 'RTB-T162', 'chore', 'RogueTraderBrevity', 87578, '2026-10-25T08:57:39.263Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-adecdb2e587aa1dc5', 'RTB-T164', 'chore', 'RogueTraderBrevity', 80217, '2026-10-25T08:57:26.676Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-adfe6651db72301a9', 'RTB-T151', 'chore', 'RogueTraderBrevity', 79172, '2026-10-25T08:38:54.319Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ae26852fb3a0410e6', 'RTB-T157', 'chore', 'RogueTraderBrevity', 72865, '2026-10-25T08:46:59.023Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ae6c95ddefcd53b13', 'RTB-T144', 'chore', 'RogueTraderBrevity', 73090, '2026-10-25T08:18:12.144Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-ae9e144c834750d08', 'RTB-T202', 'chore', 'RogueTraderBrevity', 93278, '2026-10-25T10:32:48.005Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aec0b836e39e5ab7d', 'RTB-T229', 'chore', 'RogueTraderBrevity', 98215, '2026-10-25T11:41:37.354Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-aee041184ee3d5a96', 'RTB-T243', 'chore', 'RogueTraderBrevity', 107252, '2026-10-25T12:12:47.768Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-af1af891d91a11a70', 'RTB-T119', 'chore', 'RogueTraderBrevity', 107094, '2026-10-25T07:46:12.756Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-af2537480cc4481e1', 'RTB-T168', 'chore', 'RogueTraderBrevity', 83441, '2026-10-25T09:07:22.352Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-af62d60096eeb7ab6', 'RTB-T115', 'chore', 'RogueTraderBrevity', 78316, '2026-10-25T07:40:16.074Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-af7be5b0d03816bd5', 'RTB-T135', 'chore', 'RogueTraderBrevity', 91338, '2026-10-25T08:04:20.937Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-afac0b861ed336ec8', 'RTB-T138', 'chore', 'RogueTraderBrevity', 130113, '2026-10-25T08:15:17.874Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-afbd8a10b1e0890ec', 'RTB-T174', 'chore', 'RogueTraderBrevity', 69149, '2026-10-25T09:18:58.035Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-afbfc75750f82969e', 'RTB-T186', 'chore', 'RogueTraderBrevity', 100827, '2026-10-25T10:00:32.902Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-afd89b14f269dabad', 'RTB-T227', 'chore', 'RogueTraderBrevity', 103050, '2026-10-25T11:37:28.265Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b74d07e9-e3e7-44d0-83dd-71a7f557c566/agent-afe00f70b1e48b5c3', 'RTB-T173', 'chore', 'RogueTraderBrevity', 82619, '2026-10-25T09:16:18.146Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a0ed11eab6327da98', 'UQD-T44', 'feature', 'Unity.QuickDuel', 300229, '2026-10-25T04:46:12.343Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a1774ea11e7beba52', 'UQD-T48', 'feature', 'Unity.QuickDuel', 280614, '2026-10-25T06:01:34.342Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a2500946b7adf9a26', 'UQD-T40', 'feature', 'Unity.QuickDuel', 234402, '2026-10-25T02:55:29.411Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a2641735d80109960', 'UQD-T41', 'feature', 'Unity.QuickDuel', 157070, '2026-10-25T03:11:16.275Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a385e3f155e733318', 'UQD-T49', 'feature', 'Unity.QuickDuel', 291236, '2026-10-25T06:20:11.522Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a39ae28f7fb3b639f', 'UQD-T45', 'feature', 'Unity.QuickDuel', 453725, '2026-10-25T05:20:52.241Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a4342ae3f39318f5e', 'UQD-T37', 'feature', 'Unity.QuickDuel', 104841, '2026-10-25T01:57:27.138Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a5458b5baefd37ff5', 'UQD-T46', 'feature', 'Unity.QuickDuel', 122698, '2026-10-25T05:36:46.107Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a561bb90e520c8f46', 'UQD-T35', 'feature', 'Unity.QuickDuel', 137721, '2026-10-25T01:29:16.145Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a60810e7af113000f', 'UQD-T50', 'feature', 'Unity.QuickDuel', 547709, '2026-10-25T06:41:44.077Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-a95ad945b4e4563c0', 'UQD-T38', 'feature', 'Unity.QuickDuel', 221000, '2026-10-25T02:16:35.788Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-aaca93ad66843fba5', 'UQD-T36', 'feature', 'Unity.QuickDuel', 245888, '2026-10-25T01:47:21.736Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-aae9b839df138289c', 'UQD-T42', 'feature', 'Unity.QuickDuel', 156180, '2026-10-25T04:12:20.503Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-ac0abcaed8ff293e4', 'UQD-T42', 'feature', 'Unity.QuickDuel', 171964, '2026-10-25T03:20:26.438Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-ac94a06299e63cd18', 'UQD-T61', 'feature', 'Unity.QuickDuel', 98323, '2026-10-25T07:05:19.827Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-ae4a0d9da43d43149', 'UQD-T47', 'feature', 'Unity.QuickDuel', 192246, '2026-10-25T05:46:37.886Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-ae4c98368344ac53f', 'UQD-T43', 'feature', 'Unity.QuickDuel', 124212, '2026-10-25T04:28:18.603Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b7b21f72-f0c3-4732-825c-40fb7b35b5f6/agent-ae5a3748493c4b4cf', 'UQD-T39', 'feature', 'Unity.QuickDuel', 142405, '2026-10-25T02:35:06.635Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a035ea269046930b4', 'M.LNCH-T83', 'fix', 'MindAttic.Launcher', 32738, '2026-10-26T16:59:01.35Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a08473194af0f18eb', 'M.ORCH-T887', 'fix', '(workspace root)', 49162, '2026-10-26T03:48:58.194Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a128f03941c7dc0c4', 'M.ORCH-T883', 'fix', 'MindAttic.Orchestrator', 48892, '2026-10-26T03:15:49.825Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a13d7c4e660b9fd37', 'M.ORCH-T958', 'fix', 'MindAttic.Orchestrator', 32089, '2026-10-26T05:52:39.433Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a1450ffbdd8a2a08e', 'M.ORCH-T841', 'fix', 'MindAttic.Orchestrator', 62068, '2026-10-26T02:30:06.257Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a16b81cd764094de7', 'M.ORCH-T967', 'fix', 'MindAttic.Orchestrator', 66601, '2026-10-26T17:23:12.932Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a2e542b9009c58aa1', 'M.ORCH-T871', 'fix', 'MindAttic.Orchestrator', 80959, '2026-10-26T02:52:01.101Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a49a76d45d8f28274', 'M.ORCH-T885', 'fix', 'MindAttic.Orchestrator', 31797, '2026-10-26T03:31:41.048Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a4f9e5ab285a36493', 'M.ORCH-T859', 'fix', 'MindAttic.Orchestrator', 32965, '2026-10-26T02:32:58.076Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a50c3a6d38ed9a466', 'M.ORCH-T870', 'fix', 'MindAttic.Orchestrator', 31028, '2026-10-26T02:34:50.226Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a53ba1461eb8676c3', 'M.LNCH-T81', 'fix', 'MindAttic.Orchestrator', 50365, '2026-10-26T16:36:12.275Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a5a6af64ec8157c20', 'M.ORCH-T868', 'fix', 'MindAttic.Orchestrator', 50637, '2026-10-26T02:34:21.033Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a73c54a88391bd7a1', 'M.ORCH-T894', 'fix', 'MindAttic.Orchestrator', 28436, '2026-10-26T04:09:09.281Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a77474f1cbf386dcb', 'M.ORCH-T901', 'fix', 'MindAttic.Orchestrator', 53706, '2026-10-26T04:40:38.075Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a82b7d5c9007630e3', 'M.ORCH-T897', 'fix', 'MindAttic.Orchestrator', 30997, '2026-10-26T04:31:29.027Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-a8e99f8821f41dad9', 'M.ORCH-T869', 'fix', 'MindAttic.Orchestrator', 89071, '2026-10-26T02:35:12.973Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-aa1dc7006000b5f9a', 'M.ORCH-T957', 'fix', 'MindAttic.Orchestrator', 45269, '2026-10-26T05:34:55.84Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-aaed73f3f12a99fac', 'M.ORCH-T895', 'fix', 'MindAttic.Orchestrator', 29483, '2026-10-26T04:09:22.738Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ab92d3b77968c84d2', 'M.ORCH-T864', 'fix', 'MindAttic.Orchestrator', 177005, '2026-10-26T02:37:29.566Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-aba121845ad96d827', 'M.ORCH-T834', 'fix', 'MindAttic.Orchestrator', 21297, '2026-10-26T02:29:42.654Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ac33482d13c9468c3', 'M.ORCH-T904', 'fix', 'MindAttic.Orchestrator', 58678, '2026-10-26T04:50:28.534Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ac8d426777de1e9af', 'M.ORCH-T852', 'fix', 'MindAttic.Orchestrator', 54564, '2026-10-26T02:33:49.197Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ad4f1130cd31bbea6', 'M.ORCH-T876', 'fix', '(workspace root)', 29360, '2026-10-26T03:04:59.531Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ad9212d57920b9174', 'M.ORCH-T877', 'fix', 'MindAttic.Orchestrator', 36502, '2026-10-26T03:09:59.495Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ada9cf786a88b0d13', 'M.ORCH-T896', 'fix', 'MindAttic.Orchestrator', 35560, '2026-10-26T04:34:41.948Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ae6d529bf2ecd694f', 'M.ORCH-T884', 'fix', 'MindAttic.Orchestrator', 110753, '2026-10-26T03:29:05.406Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-ae8039c0d5cee03c6', 'M.ORCH-T959', 'fix', 'MindAttic.Orchestrator', 284023, '2026-10-26T21:39:16.658Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-af4b9b0245460a4c7', 'M.ORCH-T886', 'fix', 'MindAttic.Orchestrator', 24967, '2026-10-26T03:32:53.669Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b8600565-24f0-4d01-900d-e3e8aad1ded0/agent-af88af8731d80d9fa', 'M.ORCH-T898', 'fix', 'MindAttic.Orchestrator', 27749, '2026-10-26T04:31:09.162Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('b932aaec-57bd-4a58-8017-954514b6a5bc', 'U.SPC-T453', 'fix', 'Unity.SpaceGod', 30707, '2026-10-23T04:23:10.931Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bb51a166-5173-4d3e-8aad-2da24c961487', 'M.CAPT-T15', 'docs', 'MindAttic.Capture', 15339, '2026-10-21T18:11:13.275Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bbaff692-95da-4fb8-a66a-886e74765235', 'GG-T29', 'chore', 'GridGameGaiden', 74311, '2026-10-16T01:35:40.176Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc14e394-4b47-47c2-98ae-8dca25d0335b', 'UQD-T51', 'chore', 'Unity.QuickDuel', 23292, '2026-10-26T04:35:03.837Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc5df24e-48f0-44b2-a625-73ccd22895f3', 'SS-T64', 'chore', 'Prose', 57700, '2026-10-15T16:42:12.843Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1', 'AGP-T4', 'feature', 'REA.Bloodlines', 656913, '2026-10-28T06:53:00.015Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-a09f05b80eb2686d5', 'RB-T246', 'feature', 'REA.Bloodlines', 169389, '2026-10-28T04:08:55.158Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-a114b2dbfbe939c9b', 'RB-T234', 'feature', 'REA.Bloodlines', 168305, '2026-10-28T06:44:37.168Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-a1228a35ac0c2cb03', 'RB-T246', 'feature', 'REA.Bloodlines', 233163, '2026-10-28T06:52:29.004Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-a4c8f0fd2b8fec2b1', 'RB-T264', 'feature', 'REA.Bloodlines', 176661, '2026-10-28T04:08:13.131Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-a7b3c25bf37a7c5a9', 'RB-T264', 'feature', 'REA.Bloodlines', 175723, '2026-10-28T06:44:58.968Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-a803bdd2807cec72e', 'AGP-T2', 'docs', 'REA.Bloodlines', 100174, '2026-10-28T04:06:24.214Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-ad6a8f0acf676bb88', 'AGP-T3', 'docs', 'REA.Bloodlines', 79119, '2026-10-28T06:42:11.389Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bc754ee7-b2e2-4870-b745-42c627ec13c1/agent-afd8f71b6b14f5077', 'RB-T234', 'feature', 'REA.Bloodlines', 190337, '2026-10-28T03:01:16.458Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bce53b3f-dde0-4412-856b-8feb6217ba2b', 'U.RICO-T134', 'fix', 'Unity.Ricochet', 23506, '2026-10-28T15:00:40.896Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bd617946-ff52-4e58-97ae-2a6cf5699b0b', 'SS-T93', 'chore', 'Prose', 460014, '2026-10-15T20:39:39.816Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bd8c4d39-d4df-435b-867b-23ae59638fa7', 'FOR-T2', 'chore', 'Formicarium', 93531, '2026-10-16T00:57:34.742Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('bde4744f-6cf3-4e60-bc02-1f7ee03cc790', 'M.AUTH-T17', 'docs', 'MindAttic.Authentication', 17982, '2026-10-21T18:11:25.493Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('be8f3e9e-f3bb-46d0-88e0-f37479bc80ce', 'RB-T187', 'fix', 'REA.Bloodlines', 7292, '2026-10-25T03:53:28.468Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c6958a98-4f1c-49ef-971b-6f63a9fa1b22', 'M.ORCH-T928', 'chore', 'MindAttic.Orchestrator', 33010, '2026-10-26T16:58:40.731Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c7681bf4-9b26-4e18-bdcc-3ce3ca3b388d', 'GG-T17', 'chore', 'GridGameGaiden', 213018, '2026-10-16T02:16:19.076Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c79db824-3a0a-4e46-a46c-d548cbfb7f5d', 'FOR-T3', 'chore', 'Formicarium', 50950, '2026-10-16T01:28:41.25Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('c9b83a5e-d92e-4b62-9070-cfd381b94189', 'RB-T61', 'fix', 'REA.Bloodlines', 30116, '2026-10-22T21:55:36.924Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cab40a13-9cef-42f2-b212-f63b8453ae0b', 'M.ORCH-T932', 'chore', 'MindAttic.Orchestrator', 20385, '2026-10-26T16:40:41.791Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cb098da3-639d-49bf-a6ea-8089096cdfa9', 'M.ORCH-T855', 'chore', 'MindAttic.Orchestrator', 60808, '2026-10-26T03:06:15.677Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cb9c1911-d0f7-4894-be18-193cf72add08', 'KP-T13', 'docs', 'KdpPublish', 16076, '2026-10-21T18:10:58.402Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cde93896-ba2d-4622-a65c-3ec191294700', 'GG-T24', 'chore', 'GridGameGaiden', 42571, '2026-10-16T01:20:19.355Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cded2062-18e3-41fd-9d4d-621f0e6b4ee0', 'RB-T59', 'fix', 'REA.Bloodlines', 20766, '2026-10-22T21:11:37.233Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('cf74dc58-d5e2-4182-be0c-b4ab3a49c988', 'GG-T32', 'chore', 'GridGameGaiden', 129183, '2026-10-15T13:17:05.986Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('d1ef7f5a-0ecd-4e23-9ead-485efdc497cc', 'M.ORCH-T1005', 'fix', 'MindAttic.Orchestrator', 29743, '2026-10-26T18:27:35.695Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-a03b8899f8b0d011e', 'UQD-T80', 'fix', '(workspace root)', 69422, '2026-10-30T02:56:14.923Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-a108a899429ace0b1', 'UQD-T83', 'fix', '(workspace root)', 60867, '2026-10-30T02:57:25.193Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-a24b4b0e4a3efd837', 'UQD-T87', 'fix', '(workspace root)', 91226, '2026-10-30T02:59:38.373Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-a4819ff16c33b5c2a', 'UQD-T82', 'fix', '(workspace root)', 54368, '2026-10-30T02:57:23.038Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-a5cb88bd22843a327', 'UQD-T86', 'fix', '(workspace root)', 39763, '2026-10-30T02:57:43.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-ac05cfa2c5970a325', 'UQD-T84', 'fix', '(workspace root)', 38931, '2026-10-30T02:57:09.945Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-adc570461ac0963da', 'UQD-T81', 'fix', '(workspace root)', 59545, '2026-10-30T02:57:12.313Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4525fa-8595-4483-b5b6-a47fad3135a0/agent-af47e897d05fd9b9a', 'UQD-T85', 'fix', '(workspace root)', 36670, '2026-10-30T02:56:56.039Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dc4addfb-4d3b-45a7-9ac2-828ff4b1782c', 'M.ORCH-T74', 'fix', 'MindAttic.Orchestrator', 52787, '2026-10-15T07:21:38.006Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd298eb1-9d24-4719-b110-46982a4ea713', 'GG-T27', 'chore', 'GridGameGaiden', 60700, '2026-10-16T00:39:19.341Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd5950c7-aab0-40f6-9e36-b0e6e19c1693', 'GG-T35', 'feature', 'GridGameGaiden', 268757, '2026-10-15T13:58:02.414Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd813c1d-56e7-4a98-bb5e-7f25a8041a2c/agent-a6c38790f53cbe880', 'UH-T77', 'feature', 'Unity.Hare', 216486, '2026-10-24T11:23:56.184Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dd8e4d1e-8aa0-4bd0-a754-73b1b2b2fa3c', 'SS-T62', 'chore', 'Prose', 191484, '2026-10-15T17:48:34.799Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddecf505-0c91-42f1-bcd2-81b044ce919e', 'GG-T31', 'chore', 'GridGameGaiden', 129061, '2026-10-15T13:03:13.336Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a01f6be6495627bae', 'M.ORCH-T793', 'feature', 'MindAttic.Orchestrator', 65211, '2026-10-24T17:28:07.817Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a04025466559bf0e3', 'M.COMP-T59', 'feature', 'MindAttic.Orchestrator', 21015, '2026-10-24T21:29:13.607Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a1058b2732f96aba1', 'GG-T199', 'fix', 'MindAttic.Orchestrator', 131286, '2026-10-25T07:15:24.108Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a19ddb3cf29473215', 'MAU-T11', 'feature', 'MindAttic.Orchestrator', 50532, '2026-10-25T06:31:35.574Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a258d993c85fe92ab', 'M.COMP-T57', 'fix', 'MindAttic.Orchestrator', 30824, '2026-10-24T17:47:40.28Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a2c975567a41f1788', 'UM-T77', 'fix', 'MindAttic.Orchestrator', 190950, '2026-10-25T07:10:16.94Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a32188c4edcbe5f23', 'UM-T74', 'docs', 'MindAttic.Orchestrator', 90441, '2026-10-25T06:34:23.158Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a334598e842ad7d50', 'UC-T44', 'fix', 'MindAttic.Orchestrator', 38147, '2026-10-25T06:58:19.006Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a33f4f5a6b654b282', 'UC-T41', 'fix', 'MindAttic.Orchestrator', 50879, '2026-10-25T06:33:00.806Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a399ef588246d2a1d', 'M.ORCH-T806', 'fix', 'MindAttic.Orchestrator', 71421, '2026-10-24T22:09:25.635Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a3eef508d237eba8c', 'M.ORCH-T786', 'feature', 'MindAttic.Orchestrator', 408808, '2026-10-24T17:29:37.989Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a45c13e4feb3db6fb', 'U.HARE-T51', 'feature', 'MindAttic.Orchestrator', 258241, '2026-10-25T07:15:08.011Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a4964a76e99faf65a', 'U.EVE-T28', 'fix', 'MindAttic.Orchestrator', 43472, '2026-10-25T07:02:16.924Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a4d3ee4e67aee79c5', 'M.ORCH-T795', 'fix', 'MindAttic.Orchestrator', 140558, '2026-10-24T17:36:11.415Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a50b31c82d4bbebc4', 'GG-T202', 'fix', 'MindAttic.Orchestrator', 47420, '2026-10-25T07:11:51.622Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a515d7466ebeca05c', 'M.ORCH-T763', 'feature', 'MindAttic.Orchestrator', 106716, '2026-10-24T13:43:18.756Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a54b9daf4f9edf26f', 'GG-T206', 'fix', 'MindAttic.Orchestrator', 70770, '2026-10-25T07:22:22.072Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a56619b003c4f5351', 'GG-T83', 'docs', 'MindAttic.Orchestrator', 55554, '2026-10-25T07:12:17.217Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a593fc34b9b4dbe2f', 'GG-T196', 'fix', 'MindAttic.Orchestrator', 110075, '2026-10-25T07:06:00.605Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a5a0896207a87c62f', 'U.EVE-T19', 'docs', 'MindAttic.Orchestrator', 77041, '2026-10-25T06:36:48.956Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a5ff5b6385714cb48', 'GG-T194', 'fix', 'MindAttic.Orchestrator', 91306, '2026-10-25T07:04:26.259Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a629fcfc5dc87e80a', 'M.ORCH-T783', 'fix', 'MindAttic.Orchestrator', 332441, '2026-10-24T16:38:35.189Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a62e660cd2a31a0ec', 'M.ORCH-T811', 'fix', 'MindAttic.Orchestrator', 170570, '2026-10-25T05:31:02.883Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a637f036bd1ead6c6', 'M.ORCH-T806', 'fix', 'MindAttic.Orchestrator', 20547, '2026-10-24T21:29:57.66Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a63ee0e0b34ce0a39', 'M.ORCH-T810', 'feature', 'MindAttic.Orchestrator', 310535, '2026-10-25T05:24:28.48Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a6cd69cd9aac095ae', 'M.ORCH-T787', 'fix', 'MindAttic.Orchestrator', 96958, '2026-10-24T16:33:02.29Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a719b2d7349b6a5ef', 'M.ORCH-T804', 'feature', 'MindAttic.Orchestrator', 201318, '2026-10-24T19:05:35.55Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a7b19b16527a34942', 'U.HARE-T50', 'feature', 'MindAttic.Orchestrator', 227177, '2026-10-25T06:47:51.762Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a7d8095644d0356f9', 'GG-T195', 'fix', 'MindAttic.Orchestrator', 36403, '2026-10-25T06:56:48.394Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a822fbe43002eea05', 'M.ORCH-T798', 'fix', 'MindAttic.Orchestrator', 77504, '2026-10-24T17:42:46.664Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a83f71dd375ba87a4', 'GG-T203', 'fix', 'MindAttic.Orchestrator', 30277, '2026-10-25T07:11:20.605Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a89ff0d3f541300d8', 'GG-T197', 'fix', 'MindAttic.Orchestrator', 64873, '2026-10-25T07:06:46.593Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a8ba1a28cc222118d', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 100723, '2026-10-24T18:45:01.234Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a8bf4106a7d7a3a08', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 98765, '2026-10-25T05:54:18.967Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a93238fcbc7f5117b', 'UM-T78', 'fix', 'MindAttic.Orchestrator', 203549, '2026-10-25T07:15:56.59Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-a9df3c94e249cd3f0', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 118777, '2026-10-24T21:37:12.685Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aa11ba19f4f6a9d33', 'UM-T80', 'fix', 'MindAttic.Orchestrator', 72815, '2026-10-25T07:13:13.232Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aa9764be281d284a7', 'M.ORCH-T784', 'fix', 'MindAttic.Orchestrator', 205617, '2026-10-24T16:34:24.92Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aab894acbfa8bb162', 'M.COMP-T54', 'fix', 'MindAttic.Orchestrator', 213122, '2026-10-24T18:03:10.023Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ab25c7a18ed122b1c', 'M.ORCH-T802', 'fix', 'MindAttic.Orchestrator', 59203, '2026-10-25T05:17:19.327Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ab4e7bad08f6fd825', 'MAU-T4', 'docs', 'MindAttic.Orchestrator', 82280, '2026-10-25T06:35:04.929Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ab51f7d6286b8b317', 'M.ORCH-T808', 'feature', 'MindAttic.Orchestrator', 195362, '2026-10-24T22:37:00.134Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ab71f178fb095aa9e', 'UM-T76', 'fix', 'MindAttic.Orchestrator', 161734, '2026-10-25T07:16:17.968Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aba35a9096bc5b9b7', 'M.ORCH-T810', 'feature', 'MindAttic.Orchestrator', 61476, '2026-10-25T05:56:53.924Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aba3a143860f1a958', 'GG-T178', 'feature', 'MindAttic.Orchestrator', 277171, '2026-10-25T06:53:43.785Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-abd51d823b4ca01f0', 'M.ORCH-T796', 'feature', 'MindAttic.Orchestrator', 102930, '2026-10-24T17:39:20.074Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-abe3b3178eed1a362', 'M.ORCH-T785', 'feature', 'MindAttic.Orchestrator', 163121, '2026-10-24T17:13:29.466Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ac6f34b719b600ff0', 'M.ORCH-T812', 'fix', 'MindAttic.Orchestrator', 375950, '2026-10-25T05:49:39.203Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ac7a62c5731c1d5b5', 'M.ORCH-T803', 'fix', 'MindAttic.Orchestrator', 144866, '2026-10-24T18:48:00.944Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ad2c55af6f398385c', 'M.COMP-T114', 'chore', 'MindAttic.Orchestrator', 133455, '2026-10-24T19:06:16.573Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ad2e2776ac35b6698', 'M.ORCH-T762', 'feature', 'MindAttic.Orchestrator', 902219, '2026-10-24T15:02:59.219Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ad827238aa9b7473b', 'M.ORCH-T809', 'fix', 'MindAttic.Orchestrator', 38579, '2026-10-24T22:52:53.189Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ad9bde8cf6c242ea6', 'M.ORCH-T805', 'feature', 'MindAttic.Orchestrator', 92290, '2026-10-24T19:03:04Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-ae9920bc91bd5d695', 'M.ORCH-T790', 'feature', 'MindAttic.Orchestrator', 467255, '2026-10-24T17:25:59.867Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aed313d6d059581d9', 'M.ORCH-T797', 'feature', 'MindAttic.Orchestrator', 111541, '2026-10-24T17:41:03.12Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-aee9af04f328fd551', 'M.ORCH-T774', 'fix', 'MindAttic.Orchestrator', 470924, '2026-10-24T18:38:19.713Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af1dfe2b5b3388474', 'MAU-T9', 'fix', 'MindAttic.Orchestrator', 140725, '2026-10-25T07:10:03.343Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af33a2188dea2a7ca', 'U.EVE-T29', 'fix', 'MindAttic.Orchestrator', 25846, '2026-10-25T07:08:47.313Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af480dbdf5f6f9a65', 'M.ORCH-T801', 'fix', 'MindAttic.Orchestrator', 279215, '2026-10-24T18:05:44.506Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af59ff3edd6059567', 'M.ORCH-T788', 'fix', 'MindAttic.Orchestrator', 290193, '2026-10-24T16:51:25.114Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af6c8e48ea32f6839', 'GG-T198', 'fix', 'MindAttic.Orchestrator', 195766, '2026-10-25T07:15:04.526Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af914a0b13d2773cd', 'GG-T179', 'feature', 'MindAttic.Orchestrator', 318014, '2026-10-25T07:16:12.875Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ddefd224-ff9a-422d-b6e9-218728189540/agent-af9b5e9306dd3d88b', 'M.ORCH-T794', 'fix', 'MindAttic.Orchestrator', 107776, '2026-10-24T17:49:27.535Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('de202566-f1b4-4c6d-a02a-a6cd5e574e04', 'M.ORCH-T959', 'fix', 'MindAttic.Orchestrator', 11487, '2026-10-26T16:14:01.874Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('df3f5e88-c88d-4191-9a78-244e5ea0434f', 'M.ORCH-T967', 'fix', 'MindAttic.Orchestrator', 18118, '2026-10-26T17:22:54.693Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a173f4a8fb2b9b60e', 'UH-T14', 'feature', 'Unity.Hare', 369989, '2026-10-23T10:54:39.407Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a452c29d99cd981bc', 'UH-T9', 'feature', 'Unity.Hare', 187568, '2026-10-23T05:51:43.855Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a474f76ba55f3bf2a', 'UH-T8', 'feature', 'Unity.Hare', 115459, '2026-10-23T05:41:51.486Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-a68bac30ccc561b32', 'UH-T15', 'feature', 'Unity.Hare', 266183, '2026-10-23T11:14:51.429Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-aadc848a6a4999e07', 'UH-T13', 'feature', 'Unity.Hare', 165707, '2026-10-23T08:48:05.041Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-ac7c3b9b9cc688def', 'UH-T10', 'feature', 'Unity.Hare', 196597, '2026-10-23T06:02:11.959Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-ad33e70b0f0ee69fd', 'UH-T11', 'feature', 'Unity.Hare', 167681, '2026-10-23T06:56:17.972Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dfd6fc55-5321-493f-bbc2-601cca3fa859/agent-adf673285c60351fd', 'UH-T12', 'feature', 'Unity.Hare', 241208, '2026-10-23T07:26:43.195Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('dff9327a-0ca8-4ec0-882d-c5d913912a33', 'M.ORCH-T659', 'fix', 'MindAttic.Orchestrator', 31979, '2026-10-23T11:04:31.842Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a090a2f3e29c161b1', 'M.TIX-T154', 'fix', 'MindAttic.Tickets', 49746, '2026-10-26T00:01:16.999Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a0919ce36e824e6d4', 'M.TIX-T155', 'fix', 'MindAttic.Tickets', 89441, '2026-10-26T00:13:11.01Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a158bce26cba34761', 'M.TIX-T75', 'feature', 'MindAttic.Tickets', 34299, '2026-10-25T22:34:35.424Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a2379f9b37f80cd5a', 'M.TIX-T78', 'feature', 'MindAttic.Tickets', 251928, '2026-10-25T21:38:05.095Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a2c18bb7aed54a17a', 'M.TIX-T88', 'feature', 'MindAttic.Tickets', 129514, '2026-10-25T23:35:07.694Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a2e8438f3ee547632', 'M.TIX-T75', 'feature', 'MindAttic.Tickets', 244934, '2026-10-25T21:38:11.569Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a2fc98c9e5f139262', 'M.TIX-T86', 'feature', 'MindAttic.Tickets', 133713, '2026-10-25T23:56:15.417Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a3586362ee9b715f4', 'M.TIX-T82', 'feature', 'MindAttic.Tickets', 205165, '2026-10-25T23:12:13.084Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a3bfbe3fc0c4b681b', 'M.TIX-T84', 'feature', 'MindAttic.Tickets', 73076, '2026-10-25T23:30:11.794Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a3de7191749813a14', 'M.TIX-T85', 'feature', 'MindAttic.Tickets', 130282, '2026-10-25T22:41:20.294Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a4d90a9aea407167b', 'M.TIX-T153', 'fix', 'MindAttic.Tickets', 107090, '2026-10-25T23:33:57.503Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a4e51fe867e7e6e69', 'M.TIX-T68', 'feature', 'MindAttic.Tickets', 133334, '2026-10-25T21:32:51.92Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a501f6fd705edd146', 'M.TIX-T76', 'feature', 'MindAttic.Tickets', 90607, '2026-10-25T21:31:16.612Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a58bc48367a0080cc', 'M.TIX-T81', 'feature', 'MindAttic.Tickets', 78669, '2026-10-25T23:12:55.61Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a5ac6037c16a7ad2c', 'M.TIX-T74', 'feature', 'MindAttic.Tickets', 92745, '2026-10-25T23:25:11.083Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a5d11624ee7868450', 'M.TIX-T79', 'feature', 'MindAttic.Tickets', 95497, '2026-10-25T23:11:42.044Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a5dde283c1fc7c442', 'M.TIX-T79', 'feature', 'MindAttic.Tickets', 183289, '2026-10-25T22:06:12.109Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a5e82c317e9484de0', 'M.TIX-T91', 'chore', 'MindAttic.Tickets', 441327, '2026-10-25T23:10:19.88Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a5ecdafd179016d0a', 'M.TIX-T82', 'feature', 'MindAttic.Tickets', 187055, '2026-10-25T22:04:51.935Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a6440fa9b6acc44a2', 'M.TIX-T71', 'chore', 'MindAttic.Tickets', 184079, '2026-10-25T21:52:25.662Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a7565461ce7482762', 'M.TIX-T67', 'feature', 'MindAttic.Tickets', 67164, '2026-10-25T21:29:22.314Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a76055163473e346d', 'M.TIX-T91', 'chore', 'MindAttic.Tickets', 146591, '2026-10-25T21:13:21.49Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a77b52ecf8090a08a', 'M.TIX-T74', 'feature', 'MindAttic.Tickets', 255623, '2026-10-25T21:38:19.189Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a7a315ce58c18878b', 'M.TIX-T74', 'feature', 'MindAttic.Tickets', 133007, '2026-10-25T22:40:49.731Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a824e64af9de92aff', 'M.TIX-T84', 'feature', 'MindAttic.Tickets', 200212, '2026-10-25T22:39:37Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a99cbad13c8bacee5', 'M.TIX-T78', 'feature', 'MindAttic.Tickets', 43220, '2026-10-25T22:38:50.166Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a9ae8b7fef9668feb', 'M.TIX-T81', 'feature', 'MindAttic.Tickets', 165587, '2026-10-25T22:06:41.034Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-a9c3fef02bf3d75fd', 'M.TIX-T82', 'feature', 'MindAttic.Tickets', 260730, '2026-10-25T23:59:59.38Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-aa087673e940417f3', 'M.TIX-T148', 'feature', 'MindAttic.Tickets', 30810, '2026-10-25T23:40:42.618Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-aa1ec122646a756b0', 'M.TIX-T66', 'feature', 'MindAttic.Tickets', 99262, '2026-10-25T21:28:57.93Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-aabf77a179f082d61', 'M.TIX-T70', 'feature', 'MindAttic.Tickets', 110517, '2026-10-25T21:32:12.942Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-aacc326a31c01c451', 'M.TIX-T77', 'feature', 'MindAttic.Tickets', 224457, '2026-10-25T21:38:06.075Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-ab0f5cbdb6c86cc54', 'M.TIX-T80', 'feature', 'MindAttic.Tickets', 94657, '2026-10-25T22:58:48.746Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-ac2308390c1eb5323', 'M.TIX-T150', 'chore', 'MindAttic.Tickets', 51376, '2026-10-25T21:17:25.606Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-acb141be57d9b5159', 'M.TIX-T73', 'feature', 'MindAttic.Tickets', 102939, '2026-10-25T22:18:42.136Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-acfc6499bd6a21031', 'M.TIX-T86', 'feature', 'MindAttic.Tickets', 207682, '2026-10-25T23:14:30.525Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-ad1f835e78c7a73b3', 'M.TIX-T80', 'feature', 'MindAttic.Tickets', 186574, '2026-10-25T22:05:22.309Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-ad83e7f557903e975', 'M.TIX-T91', 'chore', 'MindAttic.Tickets', 198818, '2026-10-25T22:05:23.871Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-ad97aa2df337756dc', 'M.TIX-T71', 'chore', 'MindAttic.Tickets', 91004, '2026-10-25T21:09:32.031Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-adb0b412be171491c', 'M.TIX-T83', 'feature', 'MindAttic.Tickets', 130987, '2026-10-25T22:06:35.191Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-adc0e11fb2af94c7f', 'M.TIX-T73', 'feature', 'MindAttic.Tickets', 234317, '2026-10-25T21:38:22.38Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-ae59c50f72a476d00', 'M.TIX-T69', 'feature', 'MindAttic.Tickets', 57054, '2026-10-25T21:30:10.507Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-af18f47be0a1f1fb9', 'M.TIX-T71', 'chore', 'MindAttic.Tickets', 225379, '2026-10-25T22:33:18.299Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-af4b079445c1c8d52', 'M.TIX-T72', 'feature', 'MindAttic.Tickets', 207850, '2026-10-25T21:36:47.503Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e08ca7df-269c-47b1-9b95-05607dcdfddc/agent-afa586b16694f8b20', 'M.TIX-T87', 'feature', 'MindAttic.Tickets', 137976, '2026-10-25T23:32:44.324Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e1115a1c-8787-44f6-9ae4-72c58c0d8c4a', 'M.ORCH-T743', 'fix', 'MindAttic.Orchestrator', 178710, '2026-10-23T20:43:01.789Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e1907ec3-2320-4f3f-a912-c6c2093eac12', 'U.RICO-T110', 'fix', 'Unity.Ricochet', 58086, '2026-10-28T02:58:09.22Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e1b11a13-af07-4894-afd8-bb45ee1b31ab', 'WF-T30', 'chore', 'MindAttic.Workflow', 370546, '2026-10-16T00:45:01.757Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e268cede-7033-44a0-9cde-56644a25b8f3', 'M.IDEA-T20', 'docs', 'MindAttic.Ideas', 17791, '2026-10-21T18:11:39.062Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e28b0263-8ef5-4c33-99f1-955d0d2acad4', 'M.ORCH-T774', 'fix', 'MindAttic.Orchestrator', 11797, '2026-10-24T18:30:40.893Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e2e3c39b-4ca7-482b-8f3a-144a5bc43033', 'U.EVE-T28', 'fix', 'Unity.Eve', 4271, '2026-10-25T07:01:37.32Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e312143d-5d79-4ec4-84d6-1da06a4944ed', 'U.SPC-T29', 'chore', 'UnitySolarSystem', 43459, '2026-10-15T09:54:26.893Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e3a783cb-464f-4dad-b7fc-d60aac35c008', 'SS-T91', 'chore', 'Prose', 232583, '2026-10-15T19:48:48.447Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e4584685-15bf-4183-a6a9-236732441d16', 'M.ORCH-T725', 'fix', 'MindAttic.Orchestrator', 18117, '2026-10-23T20:23:15.97Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e4a0cb4d-fb7a-4e24-9357-e276c4f1747d', 'U.RICO-T25', 'fix', 'Unity.Ricochet', 35600, '2026-10-26T06:36:54.406Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e54b58e9-efef-4e6e-a84b-c369be34b4e7', 'U.SPC-T559', 'fix', 'Unity.SpaceGod', 56883, '2026-10-25T04:50:26.371Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e653ecca-2b4a-4e49-8c9a-a2070d965550', 'M.PSST-T16', 'docs', 'MindAttic.Psst', 18693, '2026-10-21T18:11:18.172Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e765c806-dd7c-4542-87fb-075508a802b3', 'U.SPC-T32', 'chore', 'UnitySolarSystem', 134712, '2026-10-15T11:07:31.908Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e836982c-1c81-453c-9f34-a86bd75201ab', 'SS-T43', 'chore', 'Prose', 159035, '2026-10-15T12:47:30.612Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('e97a0688-750c-4157-9ff6-802b3460b327', 'GG-T33', 'chore', 'GridGameGaiden', 155465, '2026-10-15T13:27:22.046Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ea19ab98-b204-4b60-882a-3ba6753d94d1', 'GG-T17', 'chore', 'GridGameGaiden', 33842, '2026-10-15T11:19:50.931Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ea2945aa-340f-411d-a243-d43a9b07e609', 'U.CHN-T45', 'chore', 'Unity.Chain', 48157, '2026-10-24T20:42:04.129Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ea9386ae-3adc-4565-ac1c-b22ebc72acee', 'U.RICO-T39', 'chore', 'Unity.Ricochet', 68537, '2026-10-26T05:48:16.301Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a155ea4b56843a5c8', 'RB-T191', 'feature', 'REA.Bloodlines', 398631, '2026-10-26T05:41:52.767Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a18fb3a4bc87c7c64', 'RB-T193', 'feature', 'REA.Bloodlines', 869781, '2026-10-26T06:24:53.067Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a19aa0f27eda618c6', 'RB-T138', 'feature', 'REA.Bloodlines', 623491, '2026-10-26T05:20:29.132Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a421a40429612b636', 'RB-T192', 'feature', 'REA.Bloodlines', 67091, '2026-10-26T05:36:26.088Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a45731a02fcf7c0a0', 'RB-T188', 'feature', 'REA.Bloodlines', 941095, '2026-10-26T04:12:32.487Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a66108834aaa0312c', 'RB-T206', 'chore', 'REA.Bloodlines', 128059, '2026-10-26T07:43:50.078Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a6fb8741fab3c9b17', 'RB-T203', 'feature', 'REA.Bloodlines', 298352, '2026-10-26T07:40:37.541Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a708b164a98acc45e', 'RB-T176', 'feature', 'REA.Bloodlines', 849913, '2026-10-26T04:21:25.058Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a81ef079b7a77309c', 'RB-T208', 'feature', 'REA.Bloodlines', 510071, '2026-10-26T21:25:00.291Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a8202d458afbb7c0c', 'RB-T172', 'chore', 'REA.Bloodlines', 345116, '2026-10-26T05:18:27.103Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a82ab382e7ccde6ee', 'RB-T214', 'fix', 'REA.Bloodlines', 69944, '2026-10-26T22:41:34.195Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a839cac48a31fdf96', 'RB-T179', 'feature', 'REA.Bloodlines', 204926, '2026-10-26T03:41:47.203Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a83b67c380d5e4cbb', 'RB-T195', 'feature', 'REA.Bloodlines', 2423053, '2026-10-26T21:18:15.93Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-a9b98d8c1d3aa517a', 'RB-T210', 'feature', 'REA.Bloodlines', 535719, '2026-10-26T21:59:25.352Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-aa4913fa7be16aa2e', 'RB-T217', 'fix', 'REA.Bloodlines', 111530, '2026-10-27T00:15:50.978Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-aad0156172e0c77d3', 'RB-T207', 'feature', 'REA.Bloodlines', 470013, '2026-10-26T08:37:07.72Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ab37e1d742ff1f2d1', 'RB-T204', 'feature', 'REA.Bloodlines', 118968, '2026-10-26T08:28:58.075Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ab407f692570462e5', 'RB-T190', 'feature', 'REA.Bloodlines', 1014413, '2026-10-26T04:48:36.996Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ab9e8dd560f30efb6', 'RB-T212', 'feature', 'REA.Bloodlines', 584692, '2026-10-26T22:35:28.709Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-abe5ea831ad5ca121', 'RB-T163', 'feature', 'REA.Bloodlines', 357537, '2026-10-26T07:45:53.365Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ad04c68f0e460b0f9', 'RB-T208', 'feature', 'REA.Bloodlines', 298030, '2026-10-26T09:18:05.908Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ad40413699db81574', 'RB-T211', 'feature', 'REA.Bloodlines', 291907, '2026-10-26T22:23:12.284Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-adedaa61719db1c4a', 'RB-T205', 'feature', 'REA.Bloodlines', 963091, '2026-10-26T08:45:53.5Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ae5873ce0d5b7fad9', 'RB-T204', 'feature', 'REA.Bloodlines', 467313, '2026-10-26T07:46:54.487Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-ae6470f33e4a73f94', 'RB-T216', 'fix', 'REA.Bloodlines', 154097, '2026-10-27T00:16:39.464Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-aec787d7c2b005c85', 'RB-T213', 'fix', 'REA.Bloodlines', 483898, '2026-10-26T23:20:23.475Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-aef4e89e305474efb', 'RB-T196', 'feature', 'REA.Bloodlines', 294440, '2026-10-26T07:23:27.007Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-af071649e08f197be', 'RB-T189', 'feature', 'REA.Bloodlines', 961171, '2026-10-26T04:28:24.121Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb0acdfa-f603-4f4a-9519-d9c5ba825666/agent-afffc6f3153c7c2e6', 'RB-T209', 'feature', 'REA.Bloodlines', 817855, '2026-10-26T22:05:13.659Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eb6901c7-4185-45f5-bf29-d3e4d806f04a', 'U.RICO-T36', 'chore', 'Unity.Ricochet', 41386, '2026-10-26T04:05:09.388Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eda91022-6573-4df8-8cb6-c35cc3d59e66', 'RB-T206', 'chore', 'REA.Bloodlines', 34207, '2026-10-26T07:43:09.414Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ee923b3d-97ef-4a2e-a598-a6fa4d2d9e47', 'M.ORCH-T591', 'fix', 'MindAttic.Orchestrator', 9257, '2026-10-21T19:29:09.662Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('eff6de0b-fb07-4fd8-881f-6cd6e857cb2f', 'U.SPC-T49', 'chore', 'Unity.SpaceGod', 69580, '2026-10-22T02:13:34.57Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f0ff95e0-534d-49ca-a0ba-a8ba14a45b67', 'SS-T86', 'chore', 'Prose', 513473, '2026-10-15T20:48:46.831Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f1629c56-25ac-474b-9147-a5195ff2346f', 'GG-T83', 'docs', 'Unity.GridGame', 18259, '2026-10-21T18:11:57.923Z', 1, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f2ae84bf-6015-4156-92ce-6959311959d4/agent-a25c291db2f10d8c1', 'M.ORCH-T832', 'fix', 'MindAttic.Orchestrator', 98412, '2026-10-26T00:49:09.466Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f2ae84bf-6015-4156-92ce-6959311959d4/agent-a964519369299df84', 'M.ORCH-T835', 'fix', 'MindAttic.Orchestrator', 83391, '2026-10-26T00:49:56.833Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f2ae84bf-6015-4156-92ce-6959311959d4/agent-a9f5cdb448bc4c1bc', 'M.ORCH-T834', 'fix', 'MindAttic.Orchestrator', 157627, '2026-10-26T00:55:52.508Z', 0, 'sonnet');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('f79e0f4c-8041-407b-ba37-72e64a52d86c', 'U.HARE-T44', 'chore', 'Unity.Hare', 57660, '2026-10-24T21:03:55.304Z', 1, 'opus');
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
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fe18b805-4bb6-47d2-af62-bf370095483b/agent-a2f0c22cbd8721e5e', 'PRS-T320', 'feature', 'Prose', 151722, '2026-10-27T22:46:51.055Z', 0, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fe18b805-4bb6-47d2-af62-bf370095483b/agent-ae85e77ef042f6ee1', 'PRS-T133', 'chore', 'Prose', 71258, '2026-10-27T22:50:03.596Z', 0, 'sonnet');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fe6b91d4-797f-45dd-bb3b-1a0fe8745bcb', 'U.HARE-T94', 'docs', 'Unity.Hare', 60164, '2026-10-28T14:30:53.153Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('fe936aaf-26ea-444e-ac86-dde15736393e', 'UR-T34', 'chore', 'Unity.Ricochet', 84914, '2026-10-23T23:37:09.162Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ffb7b0e5-451a-4d53-beb9-5d3f0fec0530', 'UNI-T21', 'fix', 'Unity.Chain', 28668, '2026-10-23T22:14:30.192Z', 1, 'opus');
INSERT INTO ticket_costs (session_id, ticket_id, type, repo, fresh, last_utc, own_share, model) VALUES ('ffd66af0-af61-4624-b5dd-1c85297d70e0', 'UQD-T23', 'fix', 'Unity.QuickDuel', 16361, '2026-10-23T10:45:13.123Z', 1, 'opus');

-- settings
INSERT INTO settings (name, value) VALUES ('AgentAbort', 'true');
INSERT INTO settings (name, value) VALUES ('AgentAbortAfterMinutes', '3');
INSERT INTO settings (name, value) VALUES ('Allocate', 'true');
INSERT INTO settings (name, value) VALUES ('AllocateHandMinutes', '15');
INSERT INTO settings (name, value) VALUES ('AllocateSkip', '[]');
INSERT INTO settings (name, value) VALUES ('Assignees', '{}');
INSERT INTO settings (name, value) VALUES ('AuditEveryHours', '24');
INSERT INTO settings (name, value) VALUES ('AutoLogin', 'false');
INSERT INTO settings (name, value) VALUES ('BlockedBy', '{}');
INSERT INTO settings (name, value) VALUES ('ContextCyclePercent', '80');
INSERT INTO settings (name, value) VALUES ('DefaultProvider', '"Claude"');
INSERT INTO settings (name, value) VALUES ('DiskCriticalGb', '5');
INSERT INTO settings (name, value) VALUES ('DiskWarnGb', '20');
INSERT INTO settings (name, value) VALUES ('ExcludeLauncherFromTeamWall', 'false');
INSERT INTO settings (name, value) VALUES ('ExemptCommands', '["/pacing","/deploy","/run"]');
INSERT INTO settings (name, value) VALUES ('Gateway', '{"Enabled":false,"Bind":"127.0.0.1","Port":7443,"DevCertNames":[],"TokenMinutes":15,"Remotes":[]}');
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
INSERT INTO settings (name, value) VALUES ('PsstAlertTo', '"ryan"');
INSERT INTO settings (name, value) VALUES ('QuietHours', '[]');
INSERT INTO settings (name, value) VALUES ('RebalanceLogins', 'false');
INSERT INTO settings (name, value) VALUES ('RepoBudgetPercent', '{}');
INSERT INTO settings (name, value) VALUES ('RepoMaxSessions', '{}');
INSERT INTO settings (name, value) VALUES ('RepoOrgs', '{}');
INSERT INTO settings (name, value) VALUES ('ReportFreshMinutes', '2');
INSERT INTO settings (name, value) VALUES ('Reservations', '{}');
INSERT INTO settings (name, value) VALUES ('ResumeAfterReset', 'true');
INSERT INTO settings (name, value) VALUES ('ReviewDiffLines', '300');
INSERT INTO settings (name, value) VALUES ('RiskyFiles', '["*AgentGate*.cs","*pace-gate*","*context-gate*","*commit-msg*","*/hooks/*","*/Budget/Pacer.cs","*/Budget/BudgetCalculator.cs","*/Budget/PaceLedger*.cs","*/Scheduling/SubagentShare.cs","*RateLimitStatus*","*/Scheduling/Dispatcher.cs","*/Scheduling/Autopilot.cs","*land*.ps1","*deploy.ps1"]');
INSERT INTO settings (name, value) VALUES ('StaleAfterHours', '12');
INSERT INTO settings (name, value) VALUES ('StoryApproved', '[]');
INSERT INTO settings (name, value) VALUES ('StoryPriorities', '{}');
INSERT INTO settings (name, value) VALUES ('SymbolTabLabels', 'true');
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
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (33, 'M.CAPT-T12', 'MindAttic.Capture', 'D:\Projects\MindAttic.worktrees\MindAttic.Capture\M.CAPT-T12', 'Claude', '2026-10-24T03:59:43.8422976Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (34, 'GG-T54', 'Unity.GridGame', 'D:\Projects\MindAttic.worktrees\Unity.GridGame\GG-T54', 'Claude', '2026-10-24T04:13:52.3179455Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (35, 'GG-T53', 'Unity.GridGame', 'D:\Projects\MindAttic.worktrees\Unity.GridGame\GG-T53', 'Claude', '2026-10-24T04:13:53.251735Z', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (36, 'MAT-T49', 'MindAttic.Agency', 'D:\Projects\MindAttic.worktrees\MindAttic.Agency\MAT-T49', 'Claude', '2026-10-24T06:38:31.351098Z', 1, NULL, NULL, NULL, 11408, '3355791e-fb1c-436b-9d8e-7fb6aeda09fd', 'sonnet', NULL, 'honey@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (37, 'AGP-T14', 'AutoGamePlay', 'D:\Projects\MindAttic\AutoGamePlay', 'Claude', '2026-10-28T17:12:44.6781502Z', 1, NULL, NULL, NULL, 57412, '9a09255a-43ca-4cfa-84d8-258b4a81afb4', 'opus', NULL, 'ohleo199@mindattic.com');
INSERT INTO assignments (position, ticket_id, repo, directory, provider, started_utc, worktree, pilot, pilot_started_utc, log, cli, cli_session, model, reviewed_utc, account) VALUES (38, 'U.OVW-T26', 'Unity.Rea.Overworld', 'D:\Projects\MindAttic\Unity.Rea.Overworld', 'Claude', '2026-10-30T15:49:09.6247648Z', 1, NULL, NULL, NULL, 3764, '12fd3110-ee17-448d-84de-55fa5e99f3f9', 'opus', NULL, 'shampoo1232@mindattic.com');

-- organizations

-- org_members

-- term_grants

-- state
INSERT INTO state (name, value) VALUES ('api-failovers', '[]');
INSERT INTO state (name, value) VALUES ('audit', '{
  "RanUtc": "2026-10-30T22:25:00.5967207Z",
  "Took": "00:00:56.2162419",
  "ReposScanned": 72,
  "FilesScanned": 8075,
  "Drift": [
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Agency",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Agency\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.014925373134328358,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.Incognito",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Incognito\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.023255813953488372,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "SyntheticExchangeTheory",
      "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.031746031746031744,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.Overworld",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Overworld\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.04081632653061224,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.ProjectVania",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.ProjectVania\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.04081632653061224,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "TownWalker",
      "Path": "D:\\Projects\\MindAttic\\TownWalker\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.044444444444444446,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.Hare",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Hare\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.05128205128205128,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.GridGame",
      "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.05405405405405406,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Eve",
      "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.07272727272727272,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "UnityRonin",
      "Path": "D:\\Projects\\MindAttic\\UnityRonin\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.07547169811320754,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Export",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Export\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.10526315789473684,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Prose",
      "Path": "D:\\Projects\\MindAttic\\Prose\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.11764705882352941,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "RusNet",
      "Path": "D:\\Projects\\MindAttic\\RusNet\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.14814814814814814,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "AutoGamePlay",
      "Path": "D:\\Projects\\MindAttic\\AutoGamePlay\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.17647058823529413,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "AutoWebNav",
      "Path": "D:\\Projects\\MindAttic\\AutoWebNav\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "KdpPublish",
      "Path": "D:\\Projects\\MindAttic\\KdpPublish\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.Chain",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Chain\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.18181818181818182,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.QuickDuel",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.QuickDuel\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.18867924528301888,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "MindAttic.Web",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.21052631578947367,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.Rea.Ricochet",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Ricochet\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.2222222222222222,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "JobHunt",
      "Path": "D:\\Projects\\MindAttic\\JobHunt\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.2857142857142857,
      "IsCustom": true
    },
    {
      "File": "AGENTS.md",
      "Repo": "RogueTrader.Overwatch",
      "Path": "D:\\Projects\\MindAttic\\RogueTrader.Overwatch\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.3333333333333333,
      "IsCustom": false
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
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.4,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "RusNet",
      "Path": "D:\\Projects\\MindAttic\\RusNet\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.4,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "RogueTrader.Shared",
      "Path": "D:\\Projects\\MindAttic\\RogueTrader.Shared\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.45454545454545453,
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
      "File": "AGENTS.md",
      "Repo": "RogueTrader.Brevity",
      "Path": "D:\\Projects\\MindAttic\\RogueTrader.Brevity\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Export",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Export\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "SyntheticExchangeTheory",
      "Path": "D:\\Projects\\MindAttic\\SyntheticExchangeTheory\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.5,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Rea.QuickDuel",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.QuickDuel\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
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
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.5555555555555556,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdItems",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdMoves",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "GTFO.EZ.AbsurdWeapons",
      "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.5882352941176471,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "TownWalker",
      "Path": "D:\\Projects\\MindAttic\\TownWalker\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Rea.Chain",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Chain\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.SpaceGod",
      "Path": "D:\\Projects\\MindAttic\\Unity.SpaceGod\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Web",
      "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.6,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Eve",
      "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.GridGame",
      "Path": "D:\\Projects\\MindAttic\\Unity.GridGame\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Rea.Incognito",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Incognito\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Rea.Overworld",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Overworld\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Unity.Rea.ProjectVania",
      "Path": "D:\\Projects\\MindAttic\\Unity.Rea.ProjectVania\\CLAUDE.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md",
      "CanonicalCount": 33,
      "Total": 46,
      "Variants": 7,
      "Similarity": 0.8,
      "IsCustom": false
    },
    {
      "File": "AGENTS.md",
      "Repo": "Unity.SpaceGod",
      "Path": "D:\\Projects\\MindAttic\\Unity.SpaceGod\\AGENTS.md",
      "CanonicalPath": "D:\\Projects\\MindAttic\\ChiMesh\\AGENTS.md",
      "CanonicalCount": 41,
      "Total": 71,
      "Variants": 31,
      "Similarity": 0.8333333333333334,
      "IsCustom": false
    }
  ],
  "Missing": [
    {
      "File": "CLAUDE.md",
      "Repo": "Automata",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "AutoWebNav",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "Formicarium",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "GTFO.EZ",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "JobHunt",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "JoyceFit",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "KdpPublish",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Cryptography",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "MindAttic.Orchestrator",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "REA.Bloodlines",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    },
    {
      "File": "CLAUDE.md",
      "Repo": "RogueTrader.Brevity",
      "HaveIt": 32,
      "Eligible": 43,
      "ExamplePath": "D:\\Projects\\MindAttic\\AutoGamePlay\\CLAUDE.md"
    }
  ],
  "Duplicates": [
    {
      "Files": [
        {
          "Repo": "Unity.Rea.Overworld",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Overworld\\Assets\\Unity.Rea.Overworld\\Core\\Content\\Json.cs",
          "Lines": 206
        },
        {
          "Repo": "Unity.Rea.ProjectVania",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.ProjectVania\\Assets\\Unity.Rea.ProjectVania\\Core\\Settings\\Json.cs",
          "Lines": 206
        },
        {
          "Repo": "Unity.Rea.Ricochet",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Ricochet\\Assets\\Rea.Ricochet\\Core\\Data\\Json.cs",
          "Lines": 206
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 206,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 3
    },
    {
      "Files": [
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
      "RepoCount": 5
    },
    {
      "Files": [
        {
          "Repo": "GTFO.EZ",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 75
        },
        {
          "Repo": "GTFO.EZ.AbsurdItems",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 75
        },
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 75
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\external\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 75
        },
        {
          "Repo": "GTFO.EZ.Shared",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.Shared\\InfiniteToggles.cs",
          "Lines": 75
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 75,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 5
    },
    {
      "Files": [
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
      "RepoCount": 5
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
          "Repo": "GTFO.EZ",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 44
        },
        {
          "Repo": "GTFO.EZ.AbsurdItems",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdItems\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 44
        },
        {
          "Repo": "GTFO.EZ.AbsurdMoves",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdMoves\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 44
        },
        {
          "Repo": "GTFO.EZ.AbsurdWeapons",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.AbsurdWeapons\\external\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 44
        },
        {
          "Repo": "GTFO.EZ.Shared",
          "Path": "D:\\Projects\\MindAttic\\GTFO.EZ.Shared\\SharedOwner.cs",
          "Lines": 44
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 44,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 5
    },
    {
      "Files": [
        {
          "Repo": "Unity.Rea.Chain",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Chain\\Assets\\Unity.Rea.Chain\\Core\\Settings\\Json.cs",
          "Lines": 269
        },
        {
          "Repo": "Unity.Rea.Overworld",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Overworld\\Assets\\Unity.Rea.Overworld\\Core\\Content\\Json.cs",
          "Lines": 206
        },
        {
          "Repo": "Unity.Rea.QuickDuel",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.QuickDuel\\Assets\\Unity.Rea.QuickDuel\\Core\\Settings\\Json.cs",
          "Lines": 206
        }
      ],
      "Identical": false,
      "Similarity": 0.945273631840796,
      "SharedLines": 201,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 3
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
      "RepoCount": 15
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
          "Repo": "Unity.Rea.Chain",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Chain\\Assets\\Unity.Rea.Chain\\Core\\Settings\\ISettingsSource.cs",
          "Lines": 34
        },
        {
          "Repo": "Unity.Rea.ProjectVania",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.ProjectVania\\Assets\\Unity.Rea.ProjectVania\\Core\\Settings\\ISettingsSource.cs",
          "Lines": 34
        },
        {
          "Repo": "Unity.Rea.QuickDuel",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.QuickDuel\\Assets\\Unity.Rea.QuickDuel\\Core\\Settings\\ISettingsSource.cs",
          "Lines": 34
        },
        {
          "Repo": "Unity.Rea.Ricochet",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Ricochet\\Assets\\Rea.Ricochet\\Core\\Data\\ISettingsSource.cs",
          "Lines": 34
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 34,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 4
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
          "Repo": "Automata",
          "Path": "D:\\Projects\\MindAttic\\Automata\\run.ps1",
          "Lines": 177
        },
        {
          "Repo": "JobHunt",
          "Path": "D:\\Projects\\MindAttic\\JobHunt\\run.ps1",
          "Lines": 177
        }
      ],
      "Identical": false,
      "Similarity": 0.6802325581395349,
      "SharedLines": 122,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\ThemeToggle\\theme-toggle.js",
          "Lines": 56
        },
        {
          "Repo": "RusNet",
          "Path": "D:\\Projects\\MindAttic\\RusNet\\rusnet\\assets\\components\\theme-toggle.js",
          "Lines": 56
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 56,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
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
          "Lines": 181
        }
      ],
      "Identical": false,
      "Similarity": 0.5743243243243243,
      "SharedLines": 90,
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
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\Button\\button.css",
          "Lines": 33
        },
        {
          "Repo": "RusNet",
          "Path": "D:\\Projects\\MindAttic\\RusNet\\rusnet\\assets\\components\\button.css",
          "Lines": 33
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 33,
      "SuggestedHome": "MindAttic.Components (web/Blazor)",
      "RepoCount": 2
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
          "Repo": "Unity.Rea.Hare",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Hare\\tools\\gml-json.ps1",
          "Lines": 27
        },
        {
          "Repo": "Unity.Rea.Ricochet",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Ricochet\\Tools\\gml-json.ps1",
          "Lines": 27
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 27,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
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
          "Repo": "MindAttic.Components",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Components\\web\\components\\ThemeToggle\\theme-toggle.css",
          "Lines": 25
        },
        {
          "Repo": "RusNet",
          "Path": "D:\\Projects\\MindAttic\\RusNet\\rusnet\\assets\\components\\theme-toggle.css",
          "Lines": 25
        }
      ],
      "Identical": true,
      "Similarity": 1,
      "SharedLines": 25,
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
          "Lines": 131
        },
        {
          "Repo": "MindAttic.Web",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Web\\ryandebraal.com\\.claude\\statusline.ps1",
          "Lines": 52
        }
      ],
      "Identical": false,
      "Similarity": 0.6595744680851063,
      "SharedLines": 36,
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
          "Repo": "Unity.Eve",
          "Path": "D:\\Projects\\MindAttic\\Unity.Eve\\tools\\launch.ps1",
          "Lines": 50
        },
        {
          "Repo": "Unity.SpaceGod",
          "Path": "D:\\Projects\\MindAttic\\Unity.SpaceGod\\tools\\launch.ps1",
          "Lines": 51
        }
      ],
      "Identical": false,
      "Similarity": 0.5555555555555556,
      "SharedLines": 30,
      "SuggestedHome": "MindAttic.Deploy or mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "MindAttic.Psst",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Psst\\.claude\\hooks\\inject-digest.ps1",
          "Lines": 48
        },
        {
          "Repo": "MindAttic.Relay",
          "Path": "D:\\Projects\\MindAttic\\MindAttic.Relay\\.claude\\hooks\\inject-digest.ps1",
          "Lines": 48
        }
      ],
      "Identical": false,
      "Similarity": 0.5581395348837209,
      "SharedLines": 29,
      "SuggestedHome": "mindattic-agent-standard",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Unity.Cursory",
          "Path": "D:\\Projects\\MindAttic\\Unity.Cursory\\Assets\\Unity.Cursory\\Tests\\PlayMode\\SessionAudioTests.cs",
          "Lines": 37
        },
        {
          "Repo": "Unity.Rea.QuickDuel",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.QuickDuel\\Assets\\Unity.Rea.QuickDuel\\Tests\\PlayMode\\SessionAudioTests.cs",
          "Lines": 43
        }
      ],
      "Identical": false,
      "Similarity": 0.6875,
      "SharedLines": 27,
      "SuggestedHome": "MindAttic.Helpers (or a new shared package)",
      "RepoCount": 2
    },
    {
      "Files": [
        {
          "Repo": "Unity.Rea.Chain",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Chain\\Assets\\Unity.Rea.Chain\\Editor\\RenderPipelineBuilder.cs",
          "Lines": 65
        },
        {
          "Repo": "Unity.Rea.ProjectVania",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.ProjectVania\\Assets\\Unity.Rea.ProjectVania\\Editor\\RenderPipelineBuilder.cs",
          "Lines": 40
        }
      ],
      "Identical": false,
      "Similarity": 0.6,
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
          "Repo": "Unity.Rea.Hare",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Hare\\tools\\unity.ps1",
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
          "Repo": "Unity.Rea.Ricochet",
          "Path": "D:\\Projects\\MindAttic\\Unity.Rea.Ricochet\\Assets\\Rea.Ricochet\\Tests\\EditMode\\GameSetupTests.cs",
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
INSERT INTO state (name, value) VALUES ('limit-resumes', '{}');
INSERT INTO state (name, value) VALUES ('limit-wakes', '{
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
    "Tested": "bb3ab37c29d9c067c84a240c20c776b66896b86a",
    "Green": "bb3ab37c29d9c067c84a240c20c776b66896b86a"
  },
  "AutoWebNav": {
    "Tested": "47b925dfd1ef9961133f91be6ee04ccf0b87c8b9"
  },
  "Cursory": {
    "Tested": "534d7658ff1d9d719ce00099c6af3c335da0a8f9"
  },
  "Formicarium": {
    "Tested": "09049c5f02e4b6c324682c958b16634120513d18"
  },
  "Gadfly": {
    "Tested": "784f59a42e6f0f65b46c9fd981308488aedd53c8"
  },
  "IdiotProof": {
    "Tested": "8baf8c21925f63d08bfc00b605ae79e319d7993a",
    "Green": "8baf8c21925f63d08bfc00b605ae79e319d7993a"
  },
  "JobHunt": {
    "Tested": "0de745678731c6be125ef8674a81f3361f1cf757",
    "Green": "0de745678731c6be125ef8674a81f3361f1cf757"
  },
  "MediaButler": {
    "Tested": "6cf3629a72e92b39c94c79287f91d9470e0e8f17"
  },
  "MindAttic.Authentication": {
    "Tested": "af97185203c7f0ecb06db1c6e71ca3fdbb43086b"
  },
  "MindAttic.Browser": {
    "Tested": "5820f43f48ce5f015cef8f645977b62825116729",
    "Green": "5820f43f48ce5f015cef8f645977b62825116729"
  },
  "MindAttic.Capture": {
    "Tested": "a60131ac623a43d0e3201576ee6f9b56fa072c12",
    "Green": "a60131ac623a43d0e3201576ee6f9b56fa072c12"
  },
  "MindAttic.Components": {
    "Tested": "edd1d0ca95967b49d147db96ee580b935b9757f2",
    "Green": "7891321c38378d715590414173915f046db654b4"
  },
  "MindAttic.Deploy": {
    "Tested": "c07f00eec0c979ab79a027934072f91083b536b1"
  },
  "MindAttic.Export": {
    "Tested": "a14329193c1e58de4551f95c8d0336a9972d8399"
  },
  "MindAttic.Helpers": {
    "Tested": "fab756aa1c5a5f7a65e1aeae3425eda41e304c89"
  },
  "MindAttic.Ideas": {
    "Tested": "4db753174c1c791ee765e533f749f9a50722c771"
  },
  "MindAttic.Launcher": {
    "Tested": "c404a7b84d3183aba22fada2d412a04920ea8a88",
    "Green": "c404a7b84d3183aba22fada2d412a04920ea8a88"
  },
  "MindAttic.Legion": {
    "Tested": "cbc1109f9e901d2030c80586a5f5d6a28c7c50b5"
  },
  "MindAttic.Log": {
    "Tested": "758afa26362ba7e6b983567e215f15229d0050a4",
    "Green": "758afa26362ba7e6b983567e215f15229d0050a4"
  },
  "MindAttic.Media": {
    "Tested": "04f558e51b43f37f2d5462d02905b6a20117c656"
  },
  "MindAttic.Orchestrator": {
    "Tested": "cc53c373c7951d418b32fc12bce666b348aa3550",
    "Green": "61956ccbf407b6c52357c55c226c40ee69d5cd59"
  },
  "MindAttic.Psst": {
    "Tested": "c737769fa2041f37d6292878e6987921516c9d90",
    "Green": "c737769fa2041f37d6292878e6987921516c9d90"
  },
  "MindAttic.Telemetry": {
    "Tested": "1ccf54098cb259c3689963a2d817cf936724bf79"
  },
  "MindAttic.Tickets": {
    "Tested": "a932f4ae9fd35176f6af8fa8279241ae6de4426e",
    "Green": "a932f4ae9fd35176f6af8fa8279241ae6de4426e"
  },
  "MindAttic.Vault": {
    "Tested": "522e11b2ff08d6c2a11ff67fe97d040d6afa4713",
    "Green": "522e11b2ff08d6c2a11ff67fe97d040d6afa4713"
  },
  "OpenCredentials": {
    "Tested": "e6fb57b5c6474ca4ed6a1ab13f2881be058ae98c"
  },
  "ProcessLogger": {
    "Tested": "2a62afce2755b24435d330b5e334ed2634b08528",
    "Green": "2a62afce2755b24435d330b5e334ed2634b08528"
  },
  "TaxRateCollector": {
    "Tested": "8e7910c36be40c3439d05c8e74b88389df5634e0"
  },
  "ThinkTank": {
    "Tested": "c2dbb144d0c9a32b9a2690870403ccac61790b61"
  },
  "Tutor": {
    "Tested": "2b8e816dbeec8fff6051b6e7fa71b76e4caf342e"
  },
  "Unity.GridGame": {
    "Tested": "0223132d1fd651281201d57401151b833fd53fa8"
  },
  "Unity.Cursory": {
    "Tested": "5ae2ddffcd0b13e473765d8df4020fd68fd14128",
    "Green": "5ae2ddffcd0b13e473765d8df4020fd68fd14128"
  },
  "REA.Bloodlines": {
    "Tested": "ab7e8ab927d76ce961d5915eb3268617f5853ab7",
    "Green": "ab7e8ab927d76ce961d5915eb3268617f5853ab7"
  },
  "ChiMesh": {
    "Tested": "14a3cdbf2ad9a423803612d0fea14b6a76b33699"
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
    "Tested": "a3d4595a69b3e3d8e5ccf7f263c8ef2cdd0724dc"
  },
  "JoyceFit": {
    "Tested": "d7b06abe41a06384dd097a9dc05fe36bdd2aa9a6"
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
    "Tested": "a463923d4c97ce4750fe3f2887f2eae078ca7146"
  },
  "MindAttic.Web": {
    "Tested": "db817ef9047d74799520f8b80d5bbf56841eb13c"
  },
  "Prose": {
    "Tested": "e5a4cf73a92608ce826b29365c196024b0cee86b"
  },
  "SyntheticExchangeTheory": {
    "Tested": "182e7c7fc7f953260be5202cc6e5317870eb1cd8"
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
    "Tested": "f58344607c758a6eb09ca299a42b64648062a1a5",
    "Green": "dfbd6a23cea37268aa5e2475ce142d1f355ad685"
  },
  "Unity.Ricochet": {
    "Tested": "cc0bb233c5568cecb229c7311f552bc6344bdac2",
    "Green": "0d6e970a9bcfea0e97287cfc226ad9656c65c306"
  },
  "Unity.SongOfDawn": {
    "Tested": "cf0ba56cfe8bbe4f709056e977efe83667c974c8"
  },
  "Unity.Overworld": {
    "Tested": "07c07a9b74309e29e7be5c39d34a38dd2d7c9c88"
  },
  "Unity.ProjectVania": {
    "Tested": "7dc97f6bb5c85e667428da61a0ba70552bc58d1b"
  },
  "RogueTraderBrevity": {
    "Tested": "55e9829f2146ed11af5bc08ab14d913b8c64ac6d"
  },
  "RusNet": {
    "Tested": "414620d39d3d394966050f041b7dec2d55c0ed2b"
  },
  "RogueTrader.Brevity": {
    "Tested": "9b98ae7041569634bca7bbef71098d9459204289"
  },
  "RogueTrader.Overwatch": {
    "Tested": "3d014c56f84ed967b9de893bef29236881eeaf3e"
  },
  "RogueTrader.Overwatch.wt-T6": {
    "Tested": "72426cbb505cb14de35e210a6d6c99a9a6c641b3"
  },
  "RogueTrader.Overwatch.wt-T8": {
    "Tested": "72426cbb505cb14de35e210a6d6c99a9a6c641b3"
  },
  "RogueTrader.Shared": {
    "Tested": "e0c1b856299ce9de50f09d53bc1af2da6121707c"
  },
  "orch-remove-pause": {
    "Tested": "6a8c49f597031fa55a3eacd15c9b890ae84c89f5"
  },
  "Unity.Incognito.wt-T42": {
    "Tested": "16543d12e59074eddab794fc37b28838d27ede4b"
  },
  "Unity.Incognito.wt-T43": {
    "Tested": "c005e95af8d045aec4fc8bc0819d5f16eb7500f0"
  },
  "MindAttic.Relay": {
    "Tested": "f9a577c3ae7a5b8ab137c454b7eff9065626f460"
  },
  "AutoGamePlay": {
    "Tested": "9d637c9b649b4038a742b0fedf7431d67aaa3726",
    "Green": "9d637c9b649b4038a742b0fedf7431d67aaa3726"
  },
  "Unity.Rea.Chain": {
    "Tested": "9a2624c4cb0ae3f289dbebd17eccd89010101832"
  },
  "Unity.Rea.Overworld": {
    "Tested": "fcbd0d3ff3734b4806082dc05024d1ee57ceb89e"
  },
  "Unity.Rea.ProjectVania": {
    "Tested": "ac10f4db9f08df61a40b3421dbddb4bb9ca987f7"
  },
  "Unity.Rea.QuickDuel": {
    "Tested": "deabb98f84cbf0bbe24de9d9cc252e289a0fe2be",
    "Green": "deabb98f84cbf0bbe24de9d9cc252e289a0fe2be"
  },
  "Unity.Rea.Hare": {
    "Tested": "da39a879915c4d4cbd792b111f51cfa8bda02302"
  },
  "Unity.Rea.Incognito": {
    "Tested": "4cbc27044b84359bb39913b96ddbce310f5b691c"
  },
  "Unity.Rea.Ricochet": {
    "Tested": "203651eb9fcf02e81bb64340155a0ee1576ff949",
    "Green": "203651eb9fcf02e81bb64340155a0ee1576ff949"
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
    "TicketId": "M.ORCH-T802",
    "Repo": "MindAttic.Orchestrator",
    "Directory": "D:\\Projects\\MindAttic.worktrees\\MindAttic.Orchestrator\\M.ORCH-T802",
    "FirstSeenUtc": "2026-10-25T07:22:44.4309354Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-25T05:50:15.1466819Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 30244,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "AGP-T14",
    "Repo": "AutoGamePlay",
    "Directory": "D:\\Projects\\MindAttic\\AutoGamePlay",
    "FirstSeenUtc": "2026-10-28T18:00:06.9714569Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-28T17:12:44.6781502Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 57412,
    "Ahead": 0,
    "Uncommitted": [],
    "Open": true
  },
  {
    "TicketId": "U.OVW-T26",
    "Repo": "Unity.Rea.Overworld",
    "Directory": "D:\\Projects\\MindAttic\\Unity.Rea.Overworld",
    "FirstSeenUtc": "2026-10-31T02:50:02.5429643Z",
    "State": "orphaned",
    "Deaths": 1,
    "Redispatches": 0,
    "DeadStartedUtc": "2026-10-30T15:49:09.6247648Z",
    "Why": "redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself",
    "Cli": 3764,
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
INSERT INTO state (name, value) VALUES ('relocate-moves', '[]');
INSERT INTO state (name, value) VALUES ('requeued', '{
  "U.RICO-T149": "2026-10-30T16:36:29.3787550Z",
  "U.CHN-T100": "2026-10-30T16:36:29.3787550Z",
  "U.RICO-T147": "2026-10-30T16:36:29.3787550Z",
  "U.OVW-T51": "2026-10-30T16:36:29.3787550Z",
  "U.RICO-T118": "2026-10-30T16:36:29.3787550Z",
  "UQD-T78": "2026-10-30T16:36:29.3787550Z",
  "U.RICO-T116": "2026-10-30T16:36:29.3787550Z",
  "U.RICO-T117": "2026-10-30T16:36:50.5873134Z",
  "U.INC-T45": "2026-10-30T16:37:06.5490351Z",
  "U.RICO-T146": "2026-10-30T16:37:06.5490351Z",
  "U.HARE-T105": "2026-10-30T16:37:46.0617698Z",
  "U.HARE-T107": "2026-10-30T16:38:14.8797105Z",
  "U.HARE-T103": "2026-10-30T16:38:55.7661868Z",
  "U.VAN-T33": "2026-10-30T16:39:22.2187285Z"
}');
INSERT INTO state (name, value) VALUES ('usage-cutoff-alerts', '{
  "Alerted": {
    "pdftransferjr@mindattic.com|week": "2026-10-25T12:00:00Z",
    "ohleo199@mindattic.com|week": "2026-10-27T22:00:00Z",
    "honey@mindattic.com|5h": "2026-10-27T08:00:00Z",
    "ryandebraal@mindattic.com|5h": "2026-10-24T05:20:00Z",
    "honey@mindattic.com|week": "2026-10-26T01:00:00Z",
    "waffle@mindattic.com|5h": "2026-10-26T07:10:00Z",
    "waffle@mindattic.com|week": "2026-10-26T16:00:00Z"
  }
}');
INSERT INTO state (name, value) VALUES ('usage-cutoffs', '{
  "Cutoffs": [
    {
      "Email": "pdftransferjr@mindattic.com",
      "Name": "Jordan",
      "WeeklyPercent": 95,
      "FiveHourPercent": 98
    },
    {
      "Email": "ohleo199@mindattic.com",
      "Name": "Erin",
      "WeeklyPercent": 75,
      "FiveHourPercent": 98
    },
    {
      "Email": "shampoo1232@mindattic.com",
      "Name": "Amy",
      "WeeklyPercent": 98,
      "FiveHourPercent": 98
    },
    {
      "Email": "honey@mindattic.com",
      "Name": "Honey",
      "WeeklyPercent": 98,
      "FiveHourPercent": 98
    },
    {
      "Email": "ryandebraal@mindattic.com",
      "Name": "Ryan",
      "WeeklyPercent": 98,
      "FiveHourPercent": 98
    },
    {
      "Email": "waffle@mindattic.com",
      "Name": "Waffle",
      "WeeklyPercent": 98,
      "FiveHourPercent": 98
    }
  ]
}');
