# XMLParser
#Author - Pavan Shegokar
### Explanation of the Code

-File Monitoring : The application uses `Timer` to monitor the input folder for new XML files every 5 second.
-Loading Reference Data : The application loads static data for value factors and emissions factors from the `ReferenceData.xml` file.
-XML Parsing : It reads the XML file and extracts data for wind, gas, and coal generators.

 **Calculations :
  -Total generation values are calculated for each generator using the formula: `Energy x Price x ValueFactor`.
  -Daily emissions are calculated for gas and coal generators using the formula: `Energy x EmissionsRating x EmissionFactor`.
  -Actual heat rates for coal generators are calculated using the formula: `TotalHeatInput / ActualNetGeneration`.

-Output Generation : The results are saved in a new XML file in the specified output folder.

### Notes
- Ensure that the paths in the `app.config` file are correctly set to your input and output directories, as well as the reference data file.
- The application will run continuously, monitoring for new files until manually stopped. 
