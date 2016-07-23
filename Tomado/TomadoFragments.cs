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

namespace Tomado {
	
	public class TimerFragment : Android.Support.V4.App.Fragment {
		//view instances
		TextView timerTextView, typeTextView;
		Button workButton, pauseButton;

		//timer logic vars
		CountDownTimer countDownTimer;
		TimerType lastTimerType;
		int lastTimerTypeInt; //int representation b/c stupid xamarin; 0 is work, 1 is short, 2 is long, -1 is pause
		bool isTimerRunning = false;
		long interval, duration = 0;
		long remainingTimeInMillis = 0;
		long minuteInMillis = 60000;
		int shortBreaks = 0;
		bool isPaused = false;
		bool firstRun = true;

		public TimerFragment() {

		}

		public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
			//return base.OnCreateView(inflater, container, savedInstanceState);

			View rootView = inflater.Inflate(Resource.Layout.Timer, container, false);

			timerTextView = rootView.FindViewById<TextView>(Resource.Id.textViewTimer);
			typeTextView = rootView.FindViewById<TextView>(Resource.Id.textViewTimerType);
			workButton = rootView.FindViewById<Button>(Resource.Id.buttonWork);
			pauseButton = rootView.FindViewById<Button>(Resource.Id.buttonPause);

			Init(savedInstanceState);

			#region button clicks

			workButton.Click += delegate {
				if (firstRun)
					firstRun = false;
				if (isPaused) {
					duration = remainingTimeInMillis;
					isPaused = false;
				}
				if (!isTimerRunning) {
					updateTimer();
					typeTextView.SetText(lastTimerType.ToString(), TextView.BufferType.Normal);
				}
				startTimer(duration);
			};
			pauseButton.Click += delegate {
				isPaused = true;
				countDownTimer.Cancel();
			};

			#endregion

			return rootView;
		}

		///helper functions to thin OnCreateView out -- copied from TimerActivity.cs; soon to be deprecated
		//sets local vars to bundle data
		private void GetBundleInfo(Bundle bundle) {
			remainingTimeInMillis = bundle.GetLong("remainingTimeInMillis");
			shortBreaks = bundle.GetInt("shortBreaks");
			isPaused = bundle.GetBoolean("isPaused");
			isTimerRunning = bundle.GetBoolean("isTimerRunning", isTimerRunning);
			lastTimerTypeInt = bundle.GetInt("lastTimerTypeInt");
			firstRun = bundle.GetBoolean("firstRun");
		}
		//converts int to TimerType enum
		private void SetTimerTypeFromInt() {
			switch (lastTimerTypeInt) {
				case -1:
					lastTimerType = TimerType.Pause;
					break;
				case 0:
					lastTimerType = TimerType.Work;
					break;
				case 1:
					lastTimerType = TimerType.ShortBreak;
					break;
				case 2:
					lastTimerType = TimerType.LongBreak;
					break;
				default:
					lastTimerType = TimerType.Work;
					break;
			}
		}
		//Initializes timer variables and sets textviews, gets state info from bundle when applicable
		private void Init(Bundle bundle) {
			interval = 500; //interval set to 500 to prevent last-second "error" with CountDownTimer
			if (bundle == null) { // just started app
				//initialize timer vars
				duration = (long)CTimer.TimerLengths.Test;
				lastTimerType = TimerType.LongBreak;

				typeTextView.SetText(TimerType.Work.ToString(), TextView.BufferType.Normal);
				timerTextView.SetText(getClockTimeLeft(duration), TextView.BufferType.Normal);
			}
			else {
				GetBundleInfo(bundle);

				SetTimerTypeFromInt();

				if (isPaused)
					timerTextView.SetText(getClockTimeLeft(remainingTimeInMillis), TextView.BufferType.Normal);

				if (remainingTimeInMillis > 0 && !isPaused) {
					startTimer(remainingTimeInMillis);
				}
				else if (!isTimerRunning || remainingTimeInMillis < interval) {
					timerTextView.SetText(Resource.String.Finished, TextView.BufferType.Normal);
				}
				typeTextView.SetText(lastTimerType.ToString(), TextView.BufferType.Normal);

			}

			if (firstRun) {
				typeTextView.SetText(TimerType.Work.ToString(), TextView.BufferType.Normal);
				timerTextView.SetText(getClockTimeLeft(CTimer.TimerLengths.Work), TextView.BufferType.Normal);
			}
		}


		//puts data in bundle
		private void SetBundleInfo(Bundle outState) {
			outState.PutLong("remainingTimeInMillis", remainingTimeInMillis);
			outState.PutInt("shortBreaks", shortBreaks);
			outState.PutBoolean("isPaused", isPaused);
			outState.PutBoolean("isTimerRunning", isTimerRunning);
			outState.PutBoolean("firstRun", firstRun);

			switch (lastTimerType) {
				case TimerType.LongBreak:
					lastTimerTypeInt = 2;
					break;
				case TimerType.ShortBreak:
					lastTimerTypeInt = 1;
					break;
				case TimerType.Work:
					lastTimerTypeInt = 0;
					break;
				case TimerType.Pause:
					lastTimerTypeInt = -1;
					break;
				default:
					lastTimerTypeInt = 0;
					break;
			}

			outState.PutInt("lastTimerTypeInt", lastTimerTypeInt);
		}

		public override void OnSaveInstanceState(Bundle outState) {
			SetBundleInfo(outState);

			base.OnSaveInstanceState(outState);
		}

		private void startTimer(long durationInMillis) {
			isTimerRunning = true;

			// make a new timer object
			countDownTimer = new CTimer(durationInMillis, interval, OnTick, OnFinish);
			countDownTimer.Start();
		}


		#region timer event functions
		//(delegated) event methods for timer to update UI
		public void OnTick(long millisUntilFinished) {
			remainingTimeInMillis = millisUntilFinished;

			//update timer textview every whole second
			if (millisUntilFinished % 1000 > interval || millisUntilFinished == duration) {
				//set timer textview, format output time to seconds
				timerTextView.SetText(getClockTimeLeft(millisUntilFinished), TextView.BufferType.Normal);
			}
		}

		public void OnFinish() {
			remainingTimeInMillis = 0;

			timerTextView.SetText("Finished", TextView.BufferType.Normal);

			isTimerRunning = false;

		}
		#endregion

		///helper functions to convert time
		private double getMinutesFromMillis(double millisUntilFinished) {
			double secsUntilFinished = getSecondsFromMillis(millisUntilFinished);
			double outputMins = (secsUntilFinished / 60) - (secsUntilFinished / 60) % 1;
			return outputMins;
		}
		private double getSecondsFromMillis(double millisUntilFinished) {

			return Math.Round(Math.Ceiling(millisUntilFinished / 1000) * 1000 * 0.001);
		}
		private string getClockTimeLeft(double minutes, double seconds) {
			string outputSecs;

			//check for last minute
			if (minutes > 0)
				seconds = seconds % (minutes * 60);

			//check for last 10 seconds
			if (seconds < 10)
				outputSecs = "0" + seconds.ToString();
			else
				outputSecs = seconds.ToString();

			return minutes.ToString() + ":" + outputSecs;

		}
		private string getClockTimeLeft(double millisUntilFinished) {
			double secsUntilFinished, minsUntilFinished;

			minsUntilFinished = getMinutesFromMillis(millisUntilFinished);
			secsUntilFinished = getSecondsFromMillis(millisUntilFinished);

			string outputTime = getClockTimeLeft(minsUntilFinished, secsUntilFinished);

			return outputTime;
		}

		//updates break info, session type, and duration
		//iterates lastTimerType through pomodoro cycle
		private void updateTimer() {
			//if you just worked, start a break
			if (lastTimerType == TimerType.Work) {
				//set appropriate break time
				if (shortBreaks < 2) {
					shortBreaks++;
					//short break
					duration = (long)CTimer.TimerLengths.ShortBreak;
					lastTimerType = TimerType.ShortBreak;
				}
				else {
					//long break
					shortBreaks = 0;
					duration = (long)CTimer.TimerLengths.LongBreak;
					lastTimerType = TimerType.LongBreak;
				}
			}

			//if you just took a break, work
			else {
				//work
				duration = (long)CTimer.TimerLengths.Work;
				lastTimerType = TimerType.Work;
			}
		}
	}

	public class SessionsFragment : Android.Support.V4.App.Fragment {
		ListView listViewSessions;
		List<Session> sessions;
		Button newSessionButton;

		public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
			//return base.OnCreateView(inflater, container, savedInstanceState);

			//get our base layout
			View rootView = inflater.Inflate(Resource.Layout.Sessions, container, false);
			
			//get view instances
			listViewSessions = rootView.FindViewById<ListView>(Resource.Id.listViewSessions);
			newSessionButton = rootView.FindViewById<Button>(Resource.Id.buttonNewSession);

			newSessionButton.Click += delegate {
				//open new session dialog fragment (TODO: implement dialog fragment)
			};

			//modify layout views
			PopulateListView();

			//return the inflated/modified base layout
			return rootView;
		}

		//creates dummy sessions
		private void PopulateListView() {
			sessions = new List<Session>();
			sessions.Add(new Session(12, 30, 1, 0, "first"));
			sessions.Add(new Session(2, 0, 3, 0, "second"));
			sessions.Add(new Session(3, 30, 5, 0, "third"));

			listViewSessions.Adapter = new SessionAdapter(Activity, sessions);
		}
	}
}