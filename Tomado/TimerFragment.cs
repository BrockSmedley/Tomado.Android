using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Clans.Fab;

namespace Tomado {

	/// <summary>
	/// Fragment to display and contain timer views & code, respectively.
	/// </summary>
	public class TimerFragment : Android.Support.V4.App.Fragment {
		//view instances
		TextView timerTextView, titleTextView, pomodorosTextView;
		FloatingActionButton workButton, finishButton;

		//notification vars
		const int timerNotificationId = 0;
		NotificationManager notificationManager;
		Notification.Builder builder;
		Notification timerNotification;
		ProgressCircleView progressCircle;
		ImageView tomadoImageView;

		//listener instance to send events
		TimerFinishListener timerFinishListener;

		//timer logic vars
		CountDownTimer countDownTimer;
		TimerType lastTimerType;
		int lastTimerTypeInt; //int representation b/c stupid xamarin; 0 is work, 1 is short, 2 is long, -1 is pause
		bool isTimerRunning = false;
		long interval, duration = 0;
		long remainingTimeInMillis = 0;
		long minuteInMillis = 60000;
		int shortBreaks = 0;
		bool isPaused = true; //it starts off paused, technically
		bool firstRun = true;
		
		Session fragmentSession; //keeps track of this timer's session info

		public interface TimerFinishListener {
			void OnTimerFinish(Session session);
		}
		
		public TimerFragment() { }

		public TimerFragment(TimerFinishListener timerFinishListener) {
			this.timerFinishListener = timerFinishListener;
		}

		public override void OnResume() {
			base.OnResume();
		}

		public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
			View rootView = inflater.Inflate(Resource.Layout.Timer, container, false);

			timerTextView = rootView.FindViewById<TextView>(Resource.Id.textViewTimer);
			titleTextView = rootView.FindViewById<TextView>(Resource.Id.textViewTimerTitle);
			pomodorosTextView = rootView.FindViewById<TextView>(Resource.Id.TextView_SessionCount);
			workButton = rootView.FindViewById<FloatingActionButton>(Resource.Id.buttonWork);
			finishButton = rootView.FindViewById<FloatingActionButton>(Resource.Id.buttonFinish);
			progressCircle = rootView.FindViewById<ProgressCircleView>(Resource.Id.progressCircle_Timer);
			tomadoImageView = rootView.FindViewById<ImageView>(Resource.Id.ImageView_tomado);

			if (fragmentSession == null) { //lone timer
				Init(savedInstanceState);
				
				fragmentSession = new Session();

				ResetTimer();

				timerTextView.Text = GetClockTimeLeft(CTimer.TimerLengths.Work);
			}
			else { //use info from session item
				Bundle bundle = new Bundle();
				string title = fragmentSession.Title;
				bundle.PutString("title", title);

				Init(bundle);
			}

			#region button clicks
			if (!workButton.HasOnClickListeners) {
				workButton.Click += delegate {
					if (firstRun) {
						remainingTimeInMillis = (long)CTimer.TimerLengths.Work;

						UpdateTimer();

						firstRun = false;

						fragmentSession.Pomodoros++;

						finishButton.Visibility = ViewStates.Visible;
					}
					if (isPaused) {
						//resume timer

						workButton.SetImageResource(Resource.Drawable.ic_pause_white_24dp);

						finishButton.Visibility = ViewStates.Visible;

						duration = remainingTimeInMillis;

						isPaused = false;

						StartTimer(duration);

						if (progressCircle.IsAnimationPaused)
							progressCircle.ResumeTimerAnimation();
						else
							progressCircle.StartTimerAnimation(duration / 1000, 0f);

						timerTextView.ClearAnimation();
					}
					else if (!isTimerRunning) {
						//new session (continuation of work)

						workButton.SetImageResource(Resource.Drawable.ic_pause_white_24dp);

						finishButton.Visibility = ViewStates.Visible;

						if (lastTimerType != TimerType.Work)
							fragmentSession.Pomodoros++;

						UpdateTimer();

						StartTimer(duration);

						progressCircle.CancelTimerAnimation();

						progressCircle.StartTimerAnimation(duration / 1000, 0f);

						timerTextView.ClearAnimation();
					}
					else {
						//pause
						workButton.SetImageResource(Resource.Drawable.ic_play_arrow_white_24dp);

						isPaused = true;

						CancelTimer();

						StartAnimationTimerPause(timerTextView);

						progressCircle.PauseTimerAnimation();
					}

					pomodorosTextView.Text = fragmentSession.Pomodoros.ToString();
					
				};
			}

			if (!finishButton.HasOnClickListeners) {
				finishButton.Click += delegate {
					finishButton.Visibility = ViewStates.Gone;
					
					//stop timer
					CancelTimer();
					
					//open congrats dialog
					ShowCongratsDialog(fragmentSession);

					timerFinishListener.OnTimerFinish(fragmentSession);

					Session session = new Session();
					SetFragmentSession(session);

					//Load up the blank task
					ResetTimer();
					timerTextView.Text = GetClockTimeLeft(CTimer.TimerLengths.Work);

					progressCircle.CancelTimerAnimation();
				};
			}
			#endregion

			return rootView;
		}
		
		//helper functions to thin OnCreateView out

		/// <summary>
		/// Sets timer running var to false and cancels timer.
		/// </summary>
		private void CancelTimer() {
			isTimerRunning = false;

			if (countDownTimer != null)
				countDownTimer.Cancel();
		}

		/// <summary>
		/// Sets local vars to bundle data.
		/// </summary>
		/// <param name="bundle"></param>
		private void SetClassTimerInfo(Bundle bundle) {
			remainingTimeInMillis = bundle.GetLong("remainingTimeInMillis");
			shortBreaks = bundle.GetInt("shortBreaks");
			isPaused = bundle.GetBoolean("isPaused");
			isTimerRunning = bundle.GetBoolean("isTimerRunning", isTimerRunning);
			lastTimerTypeInt = bundle.GetInt("lastTimerTypeInt");
			firstRun = bundle.GetBoolean("firstRun");
		}

		/// <summary>
		/// Converts int to TimerType enum.
		/// </summary>
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

		/// <summary>
		/// Initializes timer variables and sets textviews, gets state info from bundle when applicable.
		/// </summary>
		/// <param name="bundle">Bundle received from a fragment method override, typically OnCreate.</param>
		private void Init(Bundle bundle) {
			interval = 500; //interval set to 500 to prevent last-second "error" with CountDownTimer

			progressCircle.CircleSize = 800;

			finishButton.Visibility = ViewStates.Gone;

			if (bundle == null) { // just started app
				SetFragmentSession(new Session() { });

				//initialize timer vars
				duration = (long)CTimer.TimerLengths.Work;
				lastTimerType = TimerType.LongBreak;//set last type to long break so that we start on work

				titleTextView.Text = fragmentSession.Title;
				timerTextView.Text = GetClockTimeLeft(duration);
			}
			else {
				SetClassTimerInfo(bundle);

				SetTimerTypeFromInt();
				
				///starts timer on activity resume
				if (isPaused)
					timerTextView.SetText(GetClockTimeLeft(remainingTimeInMillis), TextView.BufferType.Normal);
				
				if (remainingTimeInMillis > 0 && !isPaused) {
					StartTimer(remainingTimeInMillis);
				}
				else if (!isTimerRunning || remainingTimeInMillis < interval)
					timerTextView.Text = Resource.String.Finished.ToString();

				if (fragmentSession == null)
					titleTextView.Text = lastTimerType.ToString();
				else
					titleTextView.Text = fragmentSession.Title;
			}

			if (firstRun) {
				titleTextView.SetText(TimerType.Work.ToString(), TextView.BufferType.Normal);
				timerTextView.SetText(GetClockTimeLeft(CTimer.TimerLengths.Work), TextView.BufferType.Normal);
			}
		}


		/// <summary>
		/// Puts timer data in bundle.
		/// </summary>
		/// <param name="outState">Bundle to populate</param>
		private Bundle SetPersistentBundleInfo(Bundle outState) {
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

			return outState;
		}

		public override void OnSaveInstanceState(Bundle outState) {
			base.OnSaveInstanceState(SetPersistentBundleInfo(outState));
		}

		void StartAnimationTimerPause(TextView timerTextView) {
			Animation anim = AnimationUtils.LoadAnimation(Context, Resource.Animation.blink);
			timerTextView.StartAnimation(anim);
		}

		/// <summary>
		/// Initializes and starts the class timer.
		/// </summary>
		/// <param name="durationInMillis"></param>
		private void StartTimer(long durationInMillis) {
			isTimerRunning = true;

			// make a new timer object
			countDownTimer = new CTimer(durationInMillis, interval, OnTick, OnFinish);
			countDownTimer.Start();

			//make intent for notification
			Intent intent = new Intent(Activity, typeof(SwipeActivity));

			//initialize notification stuff
			InitNotificationVars("Tomado timer", "Timer is running", Resource.Drawable.Icon, true, intent);

			//publish notification
			notificationManager.Notify(timerNotificationId, timerNotification);
		}

		/// <summary>
		/// Initializes class variables for notification management: builder, timerNotification, & notificationManager
		/// </summary>
		/// <param name="title"></param>
		/// <param name="content"></param>
		/// <param name="icon"></param>
		/// <param name="ongoing"></param>
		private void InitNotificationVars(string title, string content, int icon, bool ongoing, Intent intent) {
			PendingIntent pendingIntent = PendingIntent.GetActivity(Activity, timerNotificationId, intent, PendingIntentFlags.OneShot, SetPersistentBundleInfo(new Bundle()));
			
			//create a notification builder
			builder = new Notification.Builder(Activity)
				.SetContentIntent(pendingIntent)
				.SetContentTitle(title)
				.SetContentText(content)
				.SetSmallIcon(icon)
				.SetOngoing(ongoing)
				.SetExtras(SetPersistentBundleInfo(new Bundle()));

			//build notification
			timerNotification = builder.Build();
			
			//get notification manager
			notificationManager = Activity.GetSystemService(Context.NotificationService) as NotificationManager;
		}

		/// <summary>
		/// Updates the timer notification text field and changes its persistence.
		/// </summary>
		/// <param name="info"></param>
		/// <param name="finished"></param>
		private void UpdateTimerNotification(string info, bool finished) {
			if (builder != null) {
				builder.SetContentText(info)
					.SetOngoing(!finished)
					.SetAutoCancel(finished);

				timerNotification = builder.Build();

				notificationManager.Notify(timerNotificationId, timerNotification);

				if (finished && this.View.IsShown) {
					notificationManager.Cancel(timerNotificationId);
				}
			}
		}

		/// <summary>
		/// Resets timer variables, then updates the text view.
		/// </summary>
		private void ResetTimer() {
			ResetTimerVars();

			//update text view
			OnFinish();
		}

		/// <summary>
		/// Resets duration, timerType, shortBreaks, and fragmentSession.Pomodoros to default value.
		/// </summary>
		private void ResetTimerVars() {
			//reset timer vars to default: work mode
			SetDuration((long)CTimer.TimerLengths.Work);
			remainingTimeInMillis = duration;
			
			SetLastTimerType(TimerType.LongBreak);

			shortBreaks = 0;
			fragmentSession.Pomodoros = 0;

			titleTextView.Text = fragmentSession.Title;
			
		}

		/// <summary>
		/// Opens a new CongratulationsDialog.
		/// </summary>
		/// <param name="session"></param>
		private void ShowCongratsDialog(Session session) {
			Android.Support.V4.App.FragmentTransaction ft = FragmentManager.BeginTransaction();

			//some code to remove any existing dialogs
			Android.Support.V4.App.Fragment prev = FragmentManager.FindFragmentByTag("dialog");
			if (prev != null) {
				ft.Remove(prev);
			}

			ft.AddToBackStack(null);

			//create and show dialog
			var dialog = new CongratulationsFragment(session);

			dialog.SetTargetFragment(this, 0);

			dialog.Show(FragmentManager, "dialog");
		}

		#region timer event handlers
		
		public void OnTick(long millisUntilFinished) {
			remainingTimeInMillis = millisUntilFinished;

			//update timer textview every whole second
			if (millisUntilFinished % 1000 > interval || millisUntilFinished == duration) {
				//set timer textview, format output time to seconds
				timerTextView.SetText(GetClockTimeLeft(millisUntilFinished), TextView.BufferType.Normal);
				UpdateTimerNotification(GetClockTimeLeft(millisUntilFinished), false);
			}
		}

		//Called when timer finishes; not when session finishes
		public void OnFinish() {
			remainingTimeInMillis = 0;

			string timerType = "";

			switch (lastTimerType) {
				case (TimerType.LongBreak):
					timerType = "Long break";
					break;
				case (TimerType.Pause):
					timerType = "Pause";
					break;
				case (TimerType.ShortBreak):
					timerType = "Short break";
					break;
				case (TimerType.Work):
					timerType = "Work";
					break;
				default:
					break;
			}

			timerTextView.SetText(timerType + " finished", TextView.BufferType.Normal);

			isTimerRunning = false;

			UpdateTimerNotification("Finished", true);

			workButton.SetImageResource(Resource.Drawable.ic_play_arrow_white_24dp);
		}

		public void OnNewTimer(Session session) {
			SetFragmentSession(session); 
			titleTextView.Text = session.Title;
			ResetTimerVars();
			timerTextView.Text = GetClockTimeLeft(CTimer.TimerLengths.Work).ToString();
		}
		#endregion

		#region helper functions to convert time
		
		/// <summary>
		/// Returns a number of minutes given a number of milliseconds.
		/// </summary>
		/// <param name="millisUntilFinished"></param>
		/// <returns></returns>
		private double getMinutesFromMillis(double millisUntilFinished) {
			double secsUntilFinished = getSecondsFromMillis(millisUntilFinished);
			double outputMins = (secsUntilFinished / 60) - (secsUntilFinished / 60) % 1;
			return outputMins;
		}
		/// <summary>
		/// Returns a number of seconds given a number of milliseconds.
		/// </summary>
		/// <param name="millisUntilFinished"></param>
		/// <returns></returns>
		private double getSecondsFromMillis(double millisUntilFinished) {

			return Math.Round(Math.Ceiling(millisUntilFinished / 1000) * 1000 * 0.001);
		}
		/// <summary>
		/// Returns a string containing how much time is left in m:ss format;
		/// </summary>
		/// <param name="minutes"></param>
		/// <param name="seconds"></param>
		/// <returns></returns>
		private string GetClockTimeLeft(double minutes, double seconds) {
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
		/// <summary>
		/// Returns a string containing how much time is left in m:ss format;
		/// </summary>
		/// <param name="minutes"></param>
		/// <param name="seconds"></param>
		/// <returns></returns>
		private string GetClockTimeLeft(double millisUntilFinished) {
			double secsUntilFinished, minsUntilFinished;

			minsUntilFinished = getMinutesFromMillis(millisUntilFinished);
			secsUntilFinished = getSecondsFromMillis(millisUntilFinished);

			string outputTime = GetClockTimeLeft(minsUntilFinished, secsUntilFinished);

			return outputTime;
		}
		#endregion

		/// <summary>
		/// Updates session type and duration as well as the short break count; iterates lastTimerType through pomodoro cycle.
		/// </summary>
		private void UpdateTimer() {
			//if you just worked, start a break
			if (lastTimerType == TimerType.Work) {
				//set appropriate break time
				if (shortBreaks < 2) {
					shortBreaks++;
					//short break
					SetDuration((long)CTimer.TimerLengths.ShortBreak);
					SetLastTimerType(TimerType.ShortBreak);
				}
				else {
					//long break
					shortBreaks = 0;
					SetDuration((long)CTimer.TimerLengths.LongBreak);
					SetLastTimerType(TimerType.LongBreak);
				}
			}

			//if you just took a break, work
			else {
				//work
				SetDuration((long)CTimer.TimerLengths.Work);
				SetLastTimerType(TimerType.Work);
			}
		}

		/// <summary>
		/// Sets the class timer's type.
		/// </summary>
		/// <param name="type"></param>
		private void SetLastTimerType(TimerType type) {
			lastTimerType = type;
		}

		/// <summary>
		/// Sets the class timer duration.
		/// </summary>
		/// <param name="duration"></param>
		private void SetDuration(long duration) {
			this.duration = duration;
		}

		/// <summary>
		/// Sets class session variable; for starting timer from session list item.
		/// </summary>
		/// <param name="session"></param>
		public void SetFragmentSession(Session session) {
			fragmentSession = session;
		}
	}
}