#pragma once
#include <string>
using namespace std;

namespace Plasma::ErrorMessage
{
	static string ContextualMessage;

	inline const string Success = "Success";
	inline const string AlreadyStarted = "The database engine is already running.";
	inline const string InvalidConfiguration = "There is an error in the database configuration.";
	inline const string ConfigNoEquals = "No equals in configuration.";
	inline const string ConfigNoKey = "No key in configuration.";
	inline const string ConfigUnrecognizedKey = "Unrecognized key in configuration.";
}
