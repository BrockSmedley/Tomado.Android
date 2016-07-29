using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;

namespace Tomado {
	/// <summary>
	/// Activity that holds multiple fragments that can be swiped through.
	/// </summary>
	[Activity(MainLauncher = true, Icon = "@drawable/icon", LaunchMode=Android.Content.PM.LaunchMode.SingleTop)]
	class SwipeActivity : FragmentActivity, SessionAdapter.SessionClickListener, TimerFragment.TimerFinishListener {
		ViewPager viewPager;
		TimerFragment timerFragment;
		SessionsFragment sessionsFragment;

		AlarmReceiver alarmReceiver;

		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.SwipeLayout);

			viewPager = FindViewById<ViewPager>(Resource.Id.viewPager);
			var adapter = new FragmentAdapter(SupportFragmentManager);

			//make fragments for swipe view
			timerFragment = new TimerFragment(this);
			sessionsFragment = new SessionsFragment(this);
			
			//add fragments to adapter
			adapter.AddFragment(timerFragment);
			adapter.AddFragment(sessionsFragment);

			//set adapter
			viewPager.Adapter = adapter;
		}

		protected override void OnStart() {
			base.OnStart();
		}

		/// <summary>
		/// Sets the visible fragment in the viewpager based on index.
		/// </summary>
		/// <param name="position"></param>
		public void SetVisibleFragment(int position) {
			viewPager.SetCurrentItem(position, true);
		}

		public void OnSessionClick(Session session) {
			//set the actual timer
			timerFragment.OnNewTimer(session);
			//set the view to timer view
			SetVisibleFragment(0);
		}

		public void OnTimerFinish(Session session) {
			if (!session.Recurring)
				sessionsFragment.OnDeleteSession(session);
		}
	}
}