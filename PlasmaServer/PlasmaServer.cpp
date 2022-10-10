#include <iostream>
#include <fstream>
#include "..\PlasmaLibrary\ErrorNumber.h"
#include "..\PlasmaLibrary\ErrorMessage.h"
#include <boost\algorithm\string\trim.hpp>
#include "..\PlasmaLibrary\DatabaseDefinitionKey.h"
#include "..\PlasmaLibrary\magic_enum.hpp"
#include "PlasmaServer.h"
#include <format>

using namespace std;

namespace Plasma::Server
{
	static Database::DatabaseDefinition* definition_;

	const int PlasmaServer::CreateNew(Database::DatabaseDefinition* definition, string definitionFileName)
	{
		ofstream configStream;
		configStream.open(definitionFileName, ofstream::out | ofstream::trunc);

		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCopyCount), definition->ServerCopyCount);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCommitCount), definition->ServerCommitCount);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::SlotPushPeriod), definition->SlotPushPeriod);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::SlotPushTriggerCount), definition->SlotPushTriggerCount);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::ClientQueryCount), definition->ClientQueryCount);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::ClientCommitCount), definition->ClientCommitCount);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCommitPeriod), definition->ServerCommitPeriod);
		configStream << format("{0}={1}\n", magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCommitTriggerCount), definition->ServerCommitTriggerCount);

		configStream.close();
		return 0;
	}

	const int PlasmaServer::Start(string definitionFileName)
	{
		if (definition_ != NULL)
			return ErrorNumber::AlreadyStarted;

		definition_ = new Database::DatabaseDefinition();

		ifstream configStream;
		configStream.open(definitionFileName, ifstream::in);

		string line;

		while (getline(configStream, line))
		{
			int result = SetDefinition(line);
			if (result != ErrorNumber::Success)
			{
				definition_ = NULL;
				return result;
			}
		}

		return ErrorNumber::Success;
	}

	const int PlasmaServer::SetDefinition(string line)
	{
		size_t split = line.find_first_of('=');

		if (split == string::npos)
		{
			ErrorMessage::ContextualMessage = "No '=' found on line: " + line;
			return ErrorNumber::ConfigNoEquals;
		}

		if (split == 0)
		{
			ErrorMessage::ContextualMessage = "No key found to left of '=' on line: " + line;
			return ErrorNumber::ConfigNoKey;
		}

		string key = line.substr(0, split - 1);
		boost::algorithm::trim(key);

		string value = line.substr(split);
		boost::algorithm::trim(value);

		if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCopyCount)) == 0)
		{
			definition_->ServerCopyCount = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCommitCount)) == 0)
		{
			definition_->ServerCommitCount = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::SlotPushPeriod)) == 0)
		{
			definition_->SlotPushPeriod = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::SlotPushTriggerCount)) == 0)
		{
			definition_->SlotPushTriggerCount = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::ClientQueryCount)) == 0)
		{
			definition_->ClientQueryCount = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::ClientCommitCount)) == 0)
		{
			definition_->ClientCommitCount = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCommitPeriod)) == 0)
		{
			definition_->ServerCommitPeriod = std::stoi(value);
		}
		else if (key.compare(magic_enum::enum_name(Database::DatabaseDefinitionKey::ServerCommitTriggerCount)) == 0)
		{
			definition_->ServerCommitTriggerCount = std::stoi(value);
		}
		else
		{
			ErrorMessage::ContextualMessage = "Unrecognized key: " + key;
			return ErrorNumber::ConfigUnrecognizedKey;
		}

		return ErrorNumber::Success;
	}

	string GetErrorText(const int errorNumber)
	{
		switch (errorNumber)
		{
		case ErrorNumber::Success:
			return ErrorMessage::Success;
		case ErrorNumber::AlreadyStarted:
			return ErrorMessage::AlreadyStarted;
		case ErrorNumber::InvalidConfiguration:
			return ErrorMessage::InvalidConfiguration;
		case ErrorNumber::ConfigNoEquals:
			return ErrorMessage::ConfigNoEquals;
		case ErrorNumber::ConfigNoKey:
			return ErrorMessage::ConfigNoKey;
		case ErrorNumber::ConfigUnrecognizedKey:
			return ErrorMessage::ConfigUnrecognizedKey;
		}

		return format("Unrecognized error number: {0}", errorNumber);
	}
}
