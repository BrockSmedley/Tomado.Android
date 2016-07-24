using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;

namespace Tomado {
	public class NewSessionFragment :  Android.Support.V4.App.DialogFragment, DatePickerDialog.IOnDateSetListener, TimePickerDialog.IOnTimeSetListener {
		private OnGetNewSessionListener onGetNewSessionListener;
		
		//view instances
		Button saveButton;
		EditText timeEditText, dateEditText, titleEditText;

		//session vars
		int _year, _month, _day, _hour, _minute;
		DateTime sessionDateTime = DateTime.Now;
		string _title;
		
		public interface OnGetNewSessionListener{
			void OnAddNewSession(DateTime dateTime, string title);
		}
		
		public override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);

			// Create your fragment here
			try {
				onGetNewSessionListener = (OnGetNewSessionListener)TargetFragment;
			}
			catch (Exception e) {
				Log.Debug("OnGetNewSessionListener cast exception", e.Message);
			}
		}

		public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
			// Use this to return your custom view for this Fragment
			// return inflater.Inflate(Resource.Layout.YourFragment, container, false);

			View view = inflater.Inflate(Resource.Layout.NewSession, container, false);

			saveButton = view.FindViewById<Button>(Resource.Id.buttonSave_NewSession);
			timeEditText = view.FindViewById<EditText>(Resource.Id.editTextTime_NewSession);
			dateEditText = view.FindViewById<EditText>(Resource.Id.editTextDate_NewSession);
			titleEditText = view.FindViewById<EditText>(Resource.Id.editTextTitle_NewSession);

			//set default values
			SetDefaultTimeValues();

			//update text views
			UpdateDateTimeInfo();

			saveButton.Click += delegate {
				///send session data back; launch event, close fragment
				//get info to save
				sessionDateTime = new DateTime(_year, _month, _day, _hour, _minute, 0);
				_title = titleEditText.Text;

				//send data out
				onGetNewSessionListener.OnAddNewSession(sessionDateTime, _title);

				//close fragment
				Dismiss();
			};
			timeEditText.Click += delegate {
				var dialog = new TimePickerDialogFragment(Activity, DateTime.Now, this);
				dialog.Show(FragmentManager, null);
			};
			dateEditText.Click += delegate {
				var dialog = new DatePickerDialogFragment(Activity, DateTime.Now, this);
				dialog.Show(FragmentManager, null);
			};

			return view;
		}

		///event handlers for date/time picker dialogs
		public void OnDateSet(DatePicker view, int year, int monthOfYear, int dayOfMonth) {
			//set time vars
			_year = year;
			_month = monthOfYear;
			_day = dayOfMonth;

			UpdateDateTimeInfo();
		}
		public void OnTimeSet(TimePicker view, int hourOfDay, int minute) {
			//set time vars
			_hour = hourOfDay;
			_minute = minute;

			UpdateDateTimeInfo();
		}

		//populate the date/time vars w/ default values
		private void SetDefaultTimeValues() {
			_year = sessionDateTime.Year;
			_month = sessionDateTime.Month;
			_day = sessionDateTime.Day;
			_hour = sessionDateTime.Hour;
			_minute = sessionDateTime.Minute;
		}

		//updates DateTime class var and the edittext views
		private void UpdateDateTimeInfo() {
			sessionDateTime = new DateTime(_year, _month, _day, _hour, _minute, 0);

			timeEditText.Text = sessionDateTime.ToShortTimeString();
			dateEditText.Text = sessionDateTime.ToShortDateString();
		}
	}
}