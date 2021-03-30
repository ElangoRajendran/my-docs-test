//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
#addin nuget:?package=Cake.FileHelpers
#addin Cake.Curl
#tool nuget:?package=Syncfusion.Spellcheck.CI
#addin nuget:?package=Octokit&version=0.50.0
using Octokit;
using System.Text.RegularExpressions
var target = Argument("target", "Default");
var reposistoryPath=MakeAbsolute(Directory("../"));
#tool nuget:?package=Syncfusion.Content.DocumentValidation.CI
#tool nuget:?package=Syncfusion.Content.FTHtmlConversion.CI
#tool nuget:?package=Syncfusion.PushGitLabToGithub
var cireports = Argument("cireports", "../cireports");
var platform=Argument<string>("platform","");
var sourcebranch=Argument<string>("branch","");
var targetBranch=Argument<string>("targetbranch","");
var Mytoken=Argument<string>("Mytoken","");
var buildStatus = true;
var isSpellingError=0;
var isDocumentvalidationError=0;
var isHtmlConversionError=0;
var isGithubMoveStatus=0;
var sourcefolder="";
var repositoryName="";
var isJobSuccess = true;

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////
using System.IO;
//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////


Task("build")
    .Does(() =>
{   
 CopyFiles("./tools/syncfusion.spellcheck.ci/Syncfusion.Spellcheck.CI/content/*", "./tools");
 CopyFiles("./tools/syncfusion.spellcheck.ci/Syncfusion.Spellcheck.CI/lib/*", "./tools");
 CopyFiles("./tools/Syncfusion.Content.DocumentValidation.CI/Syncfusion.Content.DocumentValidation.CI/content/*", "./");
 CopyFiles("./tools/Syncfusion.Content.DocumentValidation.CI/Syncfusion.Content.DocumentValidation.CI/lib/*", "./");
 CopyFiles("./tools/Syncfusion.Content.FTHtmlConversion.CI/Syncfusion.Content.FTHtmlConversion.CI/content/*", "./");
 CopyFiles("./tools/Syncfusion.Content.FTHtmlConversion.CI/Syncfusion.Content.FTHtmlConversion.CI/lib/*", "./");
 CopyFiles("./tools/Syncfusion.PushGitLabToGithub/Syncfusion.PushGitLabToGithub/tools/*", "./tools");
 EnsureDirectoryExists("./Templates");
 CopyFiles("./tools/Syncfusion.Content.DocumentValidation.CI/Syncfusion.Content.DocumentValidation.CI/Templates/*", "./Templates");
 EnsureDirectoryExists("./HtmlConvertionTemplates");
 CopyFiles("./tools/Syncfusion.Content.FTHtmlConversion.CI/Syncfusion.Content.FTHtmlConversion.CI/HtmlConvertionTemplates/*", "./HtmlConvertionTemplates");
  
  
  var directories = GetSubDirectories(reposistoryPath);
  foreach(var repository in directories)
    {
	 if(!repository.ToString().Contains("ug_spellchecker")&&!repository.ToString().Contains("cireports"))
	 {
	  sourcefolder=repository.ToString();
	 }
    }
    try
    {
        //Code to run spellchecker tool
        isSpellingError=StartProcess("./tools/DocumentSpellChecker.exe",new ProcessSettings{ Arguments = "/IsCIOperation:true /platform:"+platform+" /branch:"+sourcebranch+" /sourcefolder:"+sourcefolder});
        
        //Code to run the Document validation tool
        repositoryName =reposistoryPath.ToString().Split('/')[3].Split('@')[0];
        isDocumentvalidationError=StartProcess("./DocumentationValidation.exe",new ProcessSettings{ Arguments = reposistoryPath+"/Spell-Checker/ "+repositoryName+" "+targetBranch});
		
	bool isWithoutError = true;

        var errorfiles = GetFiles("../cireports/errorlogs/*.txt");
		
	if(!(errorfiles.Count() > 0))
        {
            var reportFiles = GetFiles(@"../cireports/**/*.(htm||html)");
				
            foreach (var reportFile in reportFiles)
            {
                string fileContent = System.IO.File.ReadAllText(reportFile.ToString());
										
                if ((fileContent.Contains("</td>")))
                {
                    if ((!reportFile.ToString().Contains("spellcheckreport")) || (fileContent.Contains("<td>Technical Error</td>") || fileContent.Contains("<td>Spell Error</td>")))
                    {
                        isWithoutError = false;
                        break;
                    }
                }
            }
            if (isWithoutError == true)
            {
		//Code to run the Html conversion tool for feature tour repositories
		if (((repositoryName.ToLower().Contains("featuretour")) && targetBranch.ToLower() == "development"))
		{
			isHtmlConversionError=StartProcess("./MDToHtmlConverter.exe",new ProcessSettings{ Arguments = reposistoryPath+"/Spell-Checker/ "+repositoryName+" "+reposistoryPath+"/FTautomation/Automation"});
		}
            }
	  }
	}
	catch(Exception ex)
	{        
		buildStatus = false;
		Information(ex);
	}
	if(isSpellingError==0 && isDocumentvalidationError==0 && isHtmlConversionError==0 && buildStatus) {    
		Information("Compilation successfull");
		RunTarget("CopyFile");
		repositoryName =reposistoryPath.ToString().Split('/')[3].Split('@')[0];
		if(targetBranch.Contains("master")&&sourcebranch.Contains("master")&& !repositoryName.ToLower().Contains("featuretour"))
		{
		  RunTarget("MoveGitlabToGithub");
		}
	} 
	else {   
		throw new Exception(String.Format("Please fix the project compilation failures"));  
	}
});

Task("CopyFile")
.Does(() =>
{
		if (!DirectoryExists(cireports))
		{			
			CreateDirectory(cireports);
		}

		EnsureDirectoryExists(cireports+"/spellcheck/");
		
		if (FileExists(cireports+"/spellcheckreport.htm"))
		{
			MoveFileToDirectory(cireports+"/spellcheckreport.htm", cireports+"/spellcheck/");
		}
		
		
});

Task("MoveGitlabToGithub")
.Does(() =>
{
	try {
            
			    repositoryName =reposistoryPath.ToString().Split('/')[3].Split('@')[0];
                Information("Moving Files from Gitlab to Github");
				Information("Cloning repository.."+repositoryName);
				Information("Cloning repository.."+reposistoryPath);
			    isGithubMoveStatus=StartProcess("./tools/PushGitLabToGithub.exe",new ProcessSettings{ Arguments = reposistoryPath+"/Spell-Checker/"+" "+repositoryName});
            
		}
	catch(Exception ex)
	{        
		buildStatus = false;
	}	
		
});


Task("GitHubCIReportValidation")
.Does(() =>
{
	try 
	{
            var errorfiles = GetFiles("../cireports/errorlogs/*.txt");
		
			if(!(errorfiles.Count() > 0))
			{
            var reportFiles = GetFiles(@"../cireports/**/*.(htm||html)");
				
				foreach (var reportFile in reportFiles)
				{
					string fileContent = System.IO.File.ReadAllText(reportFile.ToString());
											
					if ((fileContent.Contains("</td>")))
					{
						if (reportFile.ToString().Contains("spellcheckreport")) 		
						{
							if (fileContent.Contains("<td>Technical Error</td>") || fileContent.Contains("<td>Spell Error</td>"))
							{
								isJobSuccess = false;
								break;
							}
							
						}
						else
						{
							isJobSuccess = false;
							break;
						}
					}
				}
            
			}
			else
			{
				isJobSuccess = false;
			}
			if (isJobSuccess == false)
			{
				throw new Exception(String.Format("Please fix the documentation errors"));  
			}
	}
	catch(Exception ex)
	{        
		Information("Job was failed due to changed documents have spelling error or document validation errors");
		throw new Exception(String.Format("Please fix the documentation errors"));
	}	
		
});

Task("PostComments")
.Does(() =>
{
	try
	{
	
	// Techincal Errors

            var technicalError = @"../cireports/spellcheck/spellcheckreport.htm";
            var technicalErrorString = FileReadText(technicalError);
            int technicalErrorMatch = Regex.Matches(technicalErrorString, "<td>Technical Error</td>").Count;
            Information("There are {0} Technical Errors exists", technicalErrorMatch);


        // Spelling Errors

            var spellingError = @"../cireports/spellcheck/spellcheckreport.htm";
            var spellingErrorString = FileReadText(spellingError);
            int spellingErrorMatch = Regex.Matches(spellingErrorString, "<td>Spell Error</td>").Count;
            Information("There are {0} Spelling Errors exists", spellingErrorMatch);
	
	
	// Front matter Error	

	    var frontMatterErrorReportPath = @"../cireports/FrontMatterValidation/FrontMatterValidation.html";
	    var frontmatterErrorString = FileReadText(frontMatterErrorReportPath);
            int frontmatterErrorMatch = Regex.Matches(frontmatterErrorString, "<tr><td style = 'border: 2px solid #416187;  color: #264c6b; padding:10px; border-collapse:collapse; border-bottom-width: 1px;'>").Count;
            Information("There are {0} Front Matter Errors exists", frontmatterErrorMatch);
	
	
	// Image Alt Text Error

            var imageAltTextError = @"../cireports/ImageValidation/AltTextValidation.html";
            var imageAltTextErrorString = FileReadText(imageAltTextError);
            int imageAltTextErrorMatch = Regex.Matches(imageAltTextErrorString, "<tr><td style = 'border: 2px solid #416187;  color: #264c6b; padding:10px; border-collapse:collapse; border-bottom-width: 1px;'>").Count;
            Information("There are {0} Image Alt Text Errors exists", imageAltTextErrorMatch);


        // Image Size Error

            var imageSizeError = @"../cireports/ImageValidation/ImageSizeValidation.html";
            var imageSizeErrorString = FileReadText(imageSizeError);
            int imageSizeErrorMatch = Regex.Matches(imageSizeErrorString, "<tr><td style = 'border: 2px solid #416187;  color: #264c6b; padding:10px; border-collapse:collapse; border-bottom-width: 1px;'>").Count;
            Information("There are {0} Image Size Errors exists", imageSizeErrorMatch);


        // Image Name Error

            var imageNameError = @"../cireports/ImageValidation/ImageNameValidation.html";
            var imageNameErrorString = FileReadText(imageNameError);
            int imageNameErrorMatch = Regex.Matches(imageNameErrorString, "<tr><td style = 'border: 2px solid #416187;  color: #264c6b; padding:10px; border-collapse:collapse; border-bottom-width: 1px;'>").Count;
            Information("There are {0} Image Name Errors exists", imageNameErrorMatch);
	
	
	// FilePath Validation

            var filePathError = @"../cireports/FilePathValidation/FilePathValidation.html";
            var filePathErrorString = FileReadText(filePathError);
            int filePathErrorMatch = Regex.Matches(filePathErrorString, "<tr><td style = 'border: 2px solid #416187;  color: #264c6b;vertical-align:top; padding:10px; border-collapse:collapse; border-bottom-width: 1px;'>").Count;
	    Information("There are {0} File Path Errors exists", filePathErrorMatch);
	
	
	// FT Layout Syntax Error

            var fTLayoutSyntaxError = @"../cireports/FTLayoutSyntaxValidation/FTStructureValidation.html";
            var fTLayoutSyntaxErrorString = FileReadText(fTLayoutSyntaxError);
            int fTLayoutSyntaxErrorMatch = Regex.Matches(fTLayoutSyntaxErrorString, "<tr><td style = 'border: 2px solid #416187;  color: #264c6b; padding:10px; border-collapse:collapse; border-bottom-width: 1px;'>").Count;
	    Information("There are {0} FT Layout Syntax Errors exists", fTLayoutSyntaxErrorMatch); 
	
	string comment = "Techincal Errors:" + technicalErrorMatch.ToString() + "\nSpelling Errors:" +spellingErrorMatch.ToString()+ "\nFront matter Errors:"+ frontmatterErrorMatch.ToString()+ "\nImage Alt Text Errors:"+ imageAltTextErrorMatch.ToString()+ "\nImage Size Errors:"+ imageSizeErrorMatch.ToString()+ "\nImage Name Errors:"+ imageNameErrorMatch.ToString()+ "\nFile Path Errors:"+ filePathErrorMatch.ToString()+ "\nFT Layout Syntax Errors:"+ fTLayoutSyntaxErrorMatch.ToString();
	
	//StartProcess(@"{curl.exe}",new ProcessSettings{ Arguments = "curl -H \"Authorization: Token 6c19e963e5cf94de1f6ae93410d53b42e22b3bba\" -X POST -d \"{ \"body\": \"My Review comments updated-Latest\" }\" \"https://api.github.com/repos/ElangoRajendran/my-docs/issues/12/comments\"" });
	
	
	//StartProcess(@"{curl}",new ProcessSettings{ Arguments = "curl" });
	
	 //StartProcess(@"{curl}",new ProcessSettings{ Arguments = '"curl -H "Authorization: Token 6c19e963e5cf94de1f6ae93410d53b42e22b3bba" -X POST -d "{ \"body\": \"Update comments via CI job\" }" "https://api.github.com/repos/ElangoRajendran/my-docs/issues/12/comments"' })
          
	
		Information(Mytoken);
		
		var github = new GitHubClient(new ProductHeaderValue("ElangoRajendran"))
		{,
		    Credentials = new Credentials(token: Mytoken),
		};

		var pullRequestNumber = 13;
		var commentBody = comment;

		github.Issue.Comment.Create("ElangoRajendran", "my-docs", pullRequestNumber, commentBody)
		    .GetAwaiter().GetResult();
	}
	catch(Exception ex)
	{
		Information(ex);
	}
	
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
